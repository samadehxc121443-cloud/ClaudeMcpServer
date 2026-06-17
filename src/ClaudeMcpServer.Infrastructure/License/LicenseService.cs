using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.Infrastructure.License;

/// <summary>
/// Validates the API key against a remote license server via HTTP.
/// After the first successful exchange the session token is cached in memory for ~1 hour,
/// so subsequent tool calls are free (no network round-trip).
/// </summary>
public sealed class LicenseService : ILicenseService
{
    private readonly LicenseSettings _settings;
    private readonly HttpClient _http;
    private readonly ILogger<LicenseService> _logger;

    // In-memory token cache — avoids a network call on every tools/call.
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _cachedClientName;
    private DateTime _tokenExpiry = DateTime.MinValue;

    /// <summary>Initializes a new instance of <see cref="LicenseService"/>.</summary>
    public LicenseService(
        IOptions<LicenseSettings> settings,
        HttpClient http,
        ILogger<LicenseService> logger)
    {
        _settings = settings.Value;
        _http = http;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<LicenseResult> ValidateAsync(CancellationToken ct)
    {
        if (_settings.DevMode)
        {
            _logger.LogWarning("License:DevMode is true — skipping validation. Disable this in production.");
            return LicenseResult.DevMode();
        }

        if (string.IsNullOrWhiteSpace(_settings.ServerUrl))
        {
            _logger.LogCritical("License:ServerUrl is not configured.");
            return LicenseResult.Invalid("License:ServerUrl must be configured. The MCP server cannot operate without a license server.");
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogError("License server URL is set but ApiKey is missing.");
            return LicenseResult.Invalid("ApiKey is required when LicenseServerUrl is configured.");
        }

        // Fast path: cached token is still valid with > 5 min remaining.
        if (_cachedClientName is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
            return LicenseResult.Valid(_cachedClientName);

        // Slow path: acquire lock to prevent multiple concurrent token exchanges.
        await _lock.WaitAsync(ct);
        try
        {
            // Double-check after acquiring the lock — another thread may have refreshed.
            if (_cachedClientName is not null && DateTime.UtcNow < _tokenExpiry.AddMinutes(-5))
                return LicenseResult.Valid(_cachedClientName);

            return await ExchangeTokenAsync(ct);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<LicenseResult> ExchangeTokenAsync(CancellationToken ct)
    {
        try
        {
            _logger.LogInformation("Exchanging API key for session token with {ServerUrl}", _settings.ServerUrl);

            var endpoint = $"{_settings.ServerUrl.TrimEnd('/')}/api/auth/token";
            var response = await _http.PostAsJsonAsync(endpoint, new { apiKey = _settings.ApiKey }, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Token exchange failed ({StatusCode}): {Body}", response.StatusCode, body);

                // Parse the error message from the server response if possible.
                var err = await TryParseError(body);
                return LicenseResult.Invalid(err ?? $"License server rejected the request ({(int)response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<TokenResponse>(ct);
            if (result?.Token is null)
                return LicenseResult.Invalid("License server returned an empty token response.");

            _cachedClientName = result.ClientName ?? "unknown";
            _tokenExpiry = result.ExpiresAt ?? DateTime.UtcNow.AddHours(1);

            _logger.LogInformation("Session token issued for {ClientName}, valid until {Expiry:u}", _cachedClientName, _tokenExpiry);
            return LicenseResult.Valid(_cachedClientName);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Could not reach license server at {ServerUrl}", _settings.ServerUrl);
            return LicenseResult.Invalid($"Could not reach license server: {ex.Message}");
        }
        catch (TaskCanceledException)
        {
            return LicenseResult.Invalid("License validation timed out.");
        }
    }

    /// <inheritdoc/>
    public async Task<UsageStatus> CheckUsageAsync(string operation, int count, CancellationToken ct)
    {
        // No tracking in dev mode or without a server — fail open.
        if (_settings.DevMode || string.IsNullOrWhiteSpace(_settings.ServerUrl) || string.IsNullOrWhiteSpace(_settings.ApiKey))
            return UsageStatus.Untracked();

        var snapshot = await QueryUsageAsync(operation, ct);
        if (snapshot is null)
            return UsageStatus.Untracked(); // server unreachable — fail open

        // The server's query reports current usage; decide locally whether the
        // requested count still fits, so we can block BEFORE sending.
        var allowed = snapshot.Limit is null || snapshot.Used + count <= snapshot.Limit.Value;
        return snapshot with { Allowed = allowed };
    }

    /// <inheritdoc/>
    public async Task<UsageStatus> RecordUsageAsync(string operation, int count, CancellationToken ct)
    {
        if (_settings.DevMode || string.IsNullOrWhiteSpace(_settings.ServerUrl) || string.IsNullOrWhiteSpace(_settings.ApiKey))
            return UsageStatus.Untracked();

        try
        {
            var endpoint = $"{_settings.ServerUrl.TrimEnd('/')}/api/usage/report";
            var response = await _http.PostAsJsonAsync(endpoint, new { apiKey = _settings.ApiKey, operation, count }, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Usage report for '{Operation}' failed ({Status}); continuing.", operation, response.StatusCode);
                return UsageStatus.Untracked();
            }

            var usage = await response.Content.ReadFromJsonAsync<UsageResponse>(ct);
            return usage is null
                ? UsageStatus.Untracked()
                : new UsageStatus(usage.Allowed, usage.Used, usage.Limit, usage.PercentUsed, Tracked: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            // Best-effort: the email already sent, so a tracking failure must not surface as an error.
            _logger.LogWarning(ex, "Could not report usage for '{Operation}'; continuing.", operation);
            return UsageStatus.Untracked();
        }
    }

    /// <summary>Queries today's usage snapshot without consuming quota. Returns null on any failure.</summary>
    private async Task<UsageStatus?> QueryUsageAsync(string operation, CancellationToken ct)
    {
        try
        {
            var endpoint = $"{_settings.ServerUrl.TrimEnd('/')}/api/usage/query";
            var response = await _http.PostAsJsonAsync(endpoint, new { apiKey = _settings.ApiKey, operation, count = 1 }, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Usage query for '{Operation}' failed ({Status}); allowing the operation.", operation, response.StatusCode);
                return null;
            }

            var usage = await response.Content.ReadFromJsonAsync<UsageResponse>(ct);
            return usage is null
                ? null
                : new UsageStatus(usage.Allowed, usage.Used, usage.Limit, usage.PercentUsed, Tracked: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Could not query usage for '{Operation}'; allowing the operation.", operation);
            return null;
        }
    }

    private static async Task<string?> TryParseError(string body)
    {
        try
        {
            var doc = System.Text.Json.JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var err))
                return err.GetString();
        }
        catch { /* ignore parse errors */ }
        return null;
    }

    private sealed record TokenResponse(
        [property: JsonPropertyName("token")]      string?   Token,
        [property: JsonPropertyName("clientName")] string?   ClientName,
        [property: JsonPropertyName("expiresAt")]  DateTime? ExpiresAt);

    private sealed record UsageResponse(
        [property: JsonPropertyName("used")]        int     Used,
        [property: JsonPropertyName("limit")]       int?    Limit,
        [property: JsonPropertyName("percentUsed")] double? PercentUsed,
        [property: JsonPropertyName("allowed")]     bool    Allowed);
}
