using ClaudeMcpServer.LicenseServer.DTOs;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.LicenseServer.Services;

/// <summary>
/// Decorator that wraps ILicenseManagerService and adds structured logging
/// around every operation without modifying the core service.
/// </summary>
public sealed class LoggingLicenseManagerService(
    ILicenseManagerService inner,
    ILogger<LoggingLicenseManagerService> logger) : ILicenseManagerService
{
    /// <inheritdoc />
    public async Task<ValidateResult> ValidateAsync(string apiKey, CancellationToken ct = default)
    {
        logger.LogInformation("Validating license key ending in ...{Suffix}", apiKey[^4..]);
        var result = await inner.ValidateAsync(apiKey, ct);
        if (result.IsValid)
            logger.LogInformation("License valid for client '{Client}'", result.ClientName);
        else
            logger.LogWarning("License validation failed: {Reason}", result.Message);
        return result;
    }

    /// <inheritdoc />
    public async Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default)
    {
        logger.LogInformation("Token exchange requested for key ending in ...{Suffix}", apiKey[^4..]);
        var result = await inner.ExchangeTokenAsync(apiKey, ct);
        logger.LogInformation("Token issued for client '{Client}', expires {ExpiresAt:u}", result.ClientName, result.ExpiresAt);
        return result;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default)
    {
        var result = await inner.GetAllKeysAsync(ct);
        logger.LogInformation("Listed {Count} license keys", result.Count);
        return result;
    }

    /// <inheritdoc />
    public async Task<CreateKeyResult> CreateKeyAsync(CreateKeyRequest req, CancellationToken ct = default)
    {
        logger.LogInformation("Creating license key for client '{Client}'", req.ClientName);
        var result = await inner.CreateKeyAsync(req, ct);
        logger.LogInformation("License key created with id {Id} for client '{Client}'", result.Id, result.ClientName);
        return result;
    }

    /// <inheritdoc />
    public async Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("Revoking license key id {Id}", id);
        var result = await inner.RevokeKeyAsync(id, ct);
        if (result is not null)
            logger.LogInformation("Revoked key id {Id} for client '{Client}'", result.Id, result.ClientName);
        else
            logger.LogWarning("Revoke failed: key id {Id} not found", id);
        return result;
    }

    /// <inheritdoc />
    public Task<int> CountKeysAsync(CancellationToken ct = default) =>
        inner.CountKeysAsync(ct);

    /// <inheritdoc />
    public async Task<bool> IsAdminKeyValidAsync(string key, CancellationToken ct = default)
    {
        var valid = await inner.IsAdminKeyValidAsync(key, ct);
        if (!valid)
            logger.LogWarning("Rejected admin key ending in ...{Suffix}", key.Length >= 4 ? key[^4..] : "????");
        return valid;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<PlanSummary>> GetActivePlansAsync(CancellationToken ct = default) =>
        inner.GetActivePlansAsync(ct);

    /// <inheritdoc />
    public async Task<PlanSummary> CreatePlanAsync(CreatePlanRequest req, CancellationToken ct = default)
    {
        logger.LogInformation("Creating plan '{Name}' (price {Price})", req.Name, req.Price);
        var result = await inner.CreatePlanAsync(req, ct);
        logger.LogInformation("Plan '{Name}' created with id {Id}", result.Name, result.Id);
        return result;
    }

    /// <inheritdoc />
    public async Task<PlanSummary?> DeactivatePlanAsync(int id, CancellationToken ct = default)
    {
        logger.LogInformation("Deactivating plan id {Id}", id);
        var result = await inner.DeactivatePlanAsync(id, ct);
        if (result is null)
            logger.LogWarning("Deactivate failed: plan id {Id} not found", id);
        return result;
    }

    /// <inheritdoc />
    public async Task<UsageResult> ReportUsageAsync(ReportUsageRequest req, CancellationToken ct = default)
    {
        var result = await inner.ReportUsageAsync(req, ct);
        if (!result.Allowed)
            logger.LogWarning("Usage BLOCKED for '{Operation}': {Used}/{Limit} (daily limit reached)",
                result.Operation, result.Used, result.Limit);
        else if (result.PercentUsed >= 90)
            logger.LogWarning("Usage at {Percent}% for '{Operation}': {Used}/{Limit}",
                result.PercentUsed, result.Operation, result.Used, result.Limit);
        else
            logger.LogInformation("Usage reported for '{Operation}': {Used}/{Limit}",
                result.Operation, result.Used, result.Limit?.ToString() ?? "∞");
        return result;
    }

    /// <inheritdoc />
    public Task<UsageResult> GetUsageTodayAsync(string apiKey, string operation, CancellationToken ct = default) =>
        inner.GetUsageTodayAsync(apiKey, operation, ct);
}
