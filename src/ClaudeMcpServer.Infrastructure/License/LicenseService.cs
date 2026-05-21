using System.Net.Http.Json;
using System.Text.Json.Serialization;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Infrastructure.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.Infrastructure.License;

/// <summary>
/// Validates the API key against a remote license server via HTTP.
/// The MCP server remains stdio-based; this is an outbound call made on startup.
/// </summary>
public sealed class LicenseService : ILicenseService
{
    private readonly LicenseSettings _settings;
    private readonly HttpClient _http;
    private readonly ILogger<LicenseService> _logger;

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
        if (string.IsNullOrWhiteSpace(_settings.ServerUrl))
        {
            _logger.LogWarning("No license server configured — running in dev mode");
            return LicenseResult.DevMode();
        }

        if (string.IsNullOrWhiteSpace(_settings.ApiKey))
        {
            _logger.LogError("License server URL is set but ApiKey is missing");
            return LicenseResult.Invalid("ApiKey is required when LicenseServerUrl is configured.");
        }

        try
        {
            _logger.LogInformation("Validating license with {ServerUrl}", _settings.ServerUrl);

            var endpoint = $"{_settings.ServerUrl.TrimEnd('/')}/api/license/validate";
            var response = await _http.PostAsJsonAsync(
                endpoint,
                new { apiKey = _settings.ApiKey },
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError("License server returned {StatusCode}: {Body}", response.StatusCode, body);
                return LicenseResult.Invalid($"License server rejected the request ({(int)response.StatusCode}).");
            }

            var result = await response.Content.ReadFromJsonAsync<LicenseResponse>(ct);
            if (result is null)
                return LicenseResult.Invalid("License server returned an empty response.");

            if (!result.Valid)
            {
                _logger.LogWarning("License invalid: {Reason}", result.Message);
                return LicenseResult.Invalid(result.Message ?? "License is not active.");
            }

            _logger.LogInformation("License valid for client: {ClientName}", result.ClientName);
            return LicenseResult.Valid(result.ClientName ?? "unknown");
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

    /// <summary>JSON contract for the license server response.</summary>
    private sealed record LicenseResponse(
        [property: JsonPropertyName("valid")]      bool   Valid,
        [property: JsonPropertyName("clientName")] string? ClientName,
        [property: JsonPropertyName("message")]    string? Message);
}
