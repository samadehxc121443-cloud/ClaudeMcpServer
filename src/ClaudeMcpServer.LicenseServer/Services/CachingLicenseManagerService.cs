using System.Text.Json;
using ClaudeMcpServer.LicenseServer.DTOs;
using Microsoft.Extensions.Caching.Distributed;

namespace ClaudeMcpServer.LicenseServer.Services;

/// <summary>
/// Decorator that caches validation results in Redis so repeated lookups
/// for the same key skip the database. Stacks on top of the core service
/// the same way LoggingLicenseManagerService does.
/// </summary>
public sealed class CachingLicenseManagerService(
    ILicenseManagerService inner,
    IDistributedCache cache,
    ILogger<CachingLicenseManagerService> logger) : ILicenseManagerService
{
    // Short TTL: a revoked/expired key may keep validating for up to this long.
    private static readonly DistributedCacheEntryOptions CacheTtl = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromSeconds(60)
    };

    public async Task<ValidateResult> ValidateAsync(string apiKey, CancellationToken ct = default)
    {
        var cacheKey = $"license:validate:{apiKey}";

        var cached = await cache.GetStringAsync(cacheKey, ct);
        if (cached is not null)
        {
            logger.LogInformation("Validation for key ending in ...{Suffix} served from Redis cache", apiKey[^4..]);
            return JsonSerializer.Deserialize<ValidateResult>(cached)!;
        }

        var result = await inner.ValidateAsync(apiKey, ct);
        await cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(result), CacheTtl, ct);
        return result;
    }

    // Token exchange creates a new session token every call — never cached.
    public Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default) =>
        inner.ExchangeTokenAsync(apiKey, ct);

    public Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default) =>
        inner.GetAllKeysAsync(ct);

    public Task<CreateKeyResult> CreateKeyAsync(CreateKeyRequest req, CancellationToken ct = default) =>
        inner.CreateKeyAsync(req, ct);

    public async Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default)
    {
        var result = await inner.RevokeKeyAsync(id, ct);
        // The cached validation entry (keyed by the API key string, unknown here)
        // expires on its own within the TTL — acceptable staleness window.
        return result;
    }

    public Task<int> CountKeysAsync(CancellationToken ct = default) =>
        inner.CountKeysAsync(ct);

    // Never cached: revoking an admin key must take effect immediately.
    public Task<bool> IsAdminKeyValidAsync(string key, CancellationToken ct = default) =>
        inner.IsAdminKeyValidAsync(key, ct);
}
