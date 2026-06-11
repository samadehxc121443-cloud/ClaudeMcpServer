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

    /// <inheritdoc />
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
    /// <inheritdoc />
    public Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default) =>
        inner.ExchangeTokenAsync(apiKey, ct);

    /// <inheritdoc />
    public Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default) =>
        inner.GetAllKeysAsync(ct);

    /// <inheritdoc />
    public Task<CreateKeyResult> CreateKeyAsync(CreateKeyRequest req, CancellationToken ct = default) =>
        inner.CreateKeyAsync(req, ct);

    /// <inheritdoc />
    public async Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default)
    {
        var result = await inner.RevokeKeyAsync(id, ct);
        // The cached validation entry (keyed by the API key string, unknown here)
        // expires on its own within the TTL — acceptable staleness window.
        return result;
    }

    /// <inheritdoc />
    public Task<int> CountKeysAsync(CancellationToken ct = default) =>
        inner.CountKeysAsync(ct);

    // Never cached: revoking an admin key must take effect immediately.
    /// <inheritdoc />
    public Task<bool> IsAdminKeyValidAsync(string key, CancellationToken ct = default) =>
        inner.IsAdminKeyValidAsync(key, ct);

    private const string PlansCacheKey = "plans:active";

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlanSummary>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var cached = await cache.GetStringAsync(PlansCacheKey, ct);
        if (cached is not null)
        {
            logger.LogInformation("Active plans served from Redis cache");
            return JsonSerializer.Deserialize<List<PlanSummary>>(cached)!;
        }

        var result = await inner.GetActivePlansAsync(ct);
        await cache.SetStringAsync(PlansCacheKey, JsonSerializer.Serialize(result), CacheTtl, ct);
        return result;
    }

    /// <inheritdoc />
    public async Task<PlanSummary> CreatePlanAsync(CreatePlanRequest req, CancellationToken ct = default)
    {
        var result = await inner.CreatePlanAsync(req, ct);
        // The plan list changed — drop the cached copy instead of waiting out the TTL.
        await cache.RemoveAsync(PlansCacheKey, ct);
        return result;
    }

    /// <inheritdoc />
    public async Task<PlanSummary?> DeactivatePlanAsync(int id, CancellationToken ct = default)
    {
        var result = await inner.DeactivatePlanAsync(id, ct);
        if (result is not null)
            await cache.RemoveAsync(PlansCacheKey, ct);
        return result;
    }
}
