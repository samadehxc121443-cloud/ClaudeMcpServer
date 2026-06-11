using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Models;
using ClaudeMcpServer.LicenseServer.Repositories;

namespace ClaudeMcpServer.LicenseServer.Services;

/// <summary>Core implementation of <see cref="ILicenseManagerService"/> backed by the repositories.</summary>
/// <param name="licenseRepo">License key data access.</param>
/// <param name="tokenRepo">Session token data access.</param>
/// <param name="adminRepo">Admin key data access.</param>
/// <param name="planRepo">Plan data access.</param>
/// <param name="usageRepo">Daily usage counter data access.</param>
/// <param name="uow">Unit of work used to persist staged changes.</param>
public sealed class LicenseManagerService(
    ILicenseKeyRepository licenseRepo,
    ISessionTokenRepository tokenRepo,
    IAdminKeyRepository adminRepo,
    IPlanRepository planRepo,
    IUsageRepository usageRepo,
    IUnitOfWork uow) : ILicenseManagerService
{
    /// <inheritdoc />
    public async Task<ValidateResult> ValidateAsync(string apiKey, CancellationToken ct = default)
    {
        var entry = await licenseRepo.GetByKeyAsync(apiKey, ct);        

        if (entry is null)
            return new ValidateResult(false, null, "License key not found.");

        if (!entry.IsActive)
            return new ValidateResult(false, entry.ClientName, "License key has been revoked.");

        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
            return new ValidateResult(false, entry.ClientName, $"License expired on {entry.ExpiresAt.Value:yyyy-MM-dd}.");

        entry.LastValidatedAt = DateTime.UtcNow;
        await uow.CommitAsync(ct);

        return new ValidateResult(true, entry.ClientName, null);
    }

    /// <inheritdoc />
    public async Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default)
    {
        var entry = await licenseRepo.GetByKeyAsync(apiKey, ct);

        if (entry is null)
            throw new UnauthorizedAccessException("License key not found.");

        if (!entry.IsActive)
            throw new UnauthorizedAccessException("License key has been revoked.");

        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
            throw new UnauthorizedAccessException($"License expired on {entry.ExpiresAt.Value:yyyy-MM-dd}.");

        await tokenRepo.RemoveExpiredForClientAsync(entry.ClientName, ct);

        var session = new SessionToken
        {
            Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
            ClientName = entry.ClientName,
            IssuedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1)
        };

        await tokenRepo.AddAsync(session, ct);
        entry.LastValidatedAt = DateTime.UtcNow;
        await uow.CommitAsync(ct);

        return new TokenResult(session.Token, session.ClientName, session.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default)
    {
        var keys = await licenseRepo.GetAllAsync(ct);
        return keys.Select(k => new KeySummary(
            k.Id, k.Key, k.ClientName, k.Notes, k.PlanName,
            k.IsActive, k.CreatedAt, k.ExpiresAt, k.LastValidatedAt))
            .ToList();
    }

    /// <inheritdoc />
    public async Task<CreateKeyResult> CreateKeyAsync(CreateKeyRequest req, CancellationToken ct = default)
    {
        Plan? plan = null;
        if (req.PlanId.HasValue)
        {
            plan = await planRepo.GetByIdAsync(req.PlanId.Value, ct);
            if (plan is null || !plan.IsActive)
                throw new ArgumentException($"Plan {req.PlanId} not found or inactive.");
        }

        // Expiry precedence: explicit date > explicit duration > plan default.
        DateTime? expiresAt = req.ExpiresAt
            ?? (req.DurationDays.HasValue ? DateTime.UtcNow.AddDays(req.DurationDays.Value) : (DateTime?)null)
            ?? (plan?.DurationDays is int days ? DateTime.UtcNow.AddDays(days) : (DateTime?)null);

        var entry = new LicenseKey
        {
            Key = Guid.NewGuid().ToString("N"),
            ClientName = req.ClientName.Trim(),
            Notes = req.Notes?.Trim(),
            PlanName = plan?.Name ?? req.PlanName?.Trim(),
            PlanId = plan?.Id,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = expiresAt
        };

        await licenseRepo.AddAsync(entry, ct);
        await uow.CommitAsync(ct);

        return new CreateKeyResult(
            entry.Id, entry.Key, entry.ClientName, entry.Notes, entry.PlanName,
            entry.IsActive, entry.CreatedAt, entry.ExpiresAt);
    }

    /// <inheritdoc />
    public async Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default)
    {
        var entry = await licenseRepo.GetByIdAsync(id, ct);
        if (entry is null) return null;

        entry.IsActive = false;
        await uow.CommitAsync(ct);

        return new RevokeResult(true, entry.Id, entry.ClientName);
    }

    /// <inheritdoc />
    public async Task<int> CountKeysAsync(CancellationToken ct = default)
    {
        var keys = await licenseRepo.GetAllAsync(ct);
        return keys.Count;
    }

    /// <inheritdoc />
    public Task<bool> IsAdminKeyValidAsync(string key, CancellationToken ct = default) =>
        adminRepo.ExistsActiveAsync(key.Trim(), ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PlanSummary>> GetActivePlansAsync(CancellationToken ct = default)
    {
        var plans = await planRepo.GetActiveAsync(ct);
        return plans.Select(ToSummary).ToList();
    }

    /// <inheritdoc />
    public async Task<PlanSummary> CreatePlanAsync(CreatePlanRequest req, CancellationToken ct = default)
    {
        var name = req.Name.Trim();
        if (await planRepo.ExistsByNameAsync(name, ct))
            throw new InvalidOperationException($"An active plan named '{name}' already exists.");

        var plan = new Plan
        {
            Name = name,
            Price = req.Price,
            MaxEmailsPerDay = req.MaxEmailsPerDay,
            DurationDays = req.DurationDays
        };

        await planRepo.AddAsync(plan, ct);
        await uow.CommitAsync(ct);
        return ToSummary(plan);
    }

    /// <inheritdoc />
    public async Task<PlanSummary?> DeactivatePlanAsync(int id, CancellationToken ct = default)
    {
        var plan = await planRepo.GetByIdAsync(id, ct);
        if (plan is null) return null;

        plan.IsActive = false;
        await uow.CommitAsync(ct);
        return ToSummary(plan);
    }

    private static PlanSummary ToSummary(Plan p) =>
        new(p.Id, p.Name, p.Price, p.MaxEmailsPerDay, p.DurationDays, p.IsActive);

    /// <inheritdoc />
    public Task<UsageResult> ReportUsageAsync(ReportUsageRequest req, CancellationToken ct = default) =>
        TrackUsageAsync(req.ApiKey, req.Operation, Math.Max(1, req.Count), ct);

    /// <inheritdoc />
    public Task<UsageResult> GetUsageTodayAsync(string apiKey, string operation, CancellationToken ct = default) =>
        TrackUsageAsync(apiKey, operation, increment: 0, ct);

    private async Task<UsageResult> TrackUsageAsync(string apiKey, string operation, int increment, CancellationToken ct)
    {
        var entry = await licenseRepo.GetByKeyAsync(apiKey, ct)
            ?? throw new UnauthorizedAccessException("License key not found.");

        if (!entry.IsActive)
            throw new UnauthorizedAccessException("License key has been revoked.");

        if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
            throw new UnauthorizedAccessException($"License expired on {entry.ExpiresAt.Value:yyyy-MM-dd}.");

        operation = operation.Trim().ToLowerInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var counter = await usageRepo.GetAsync(entry.Id, today, operation, ct);
        var used = counter?.Count ?? 0;

        // Limits are parametrization data: they come from the key's plan in the
        // database. Only "email" is metered against a plan limit for now.
        int? limit = null;
        if (operation == "email" && entry.PlanId.HasValue)
        {
            var plan = await planRepo.GetByIdAsync(entry.PlanId.Value, ct);
            limit = plan?.MaxEmailsPerDay;
        }

        var allowed = limit is null || used + increment <= limit.Value;

        if (allowed && increment > 0)
        {
            if (counter is null)
            {
                counter = new DailyUsage { LicenseKeyId = entry.Id, Date = today, Operation = operation, Count = increment };
                await usageRepo.AddAsync(counter, ct);
            }
            else
            {
                counter.Count += increment;
            }
            await uow.CommitAsync(ct);
            used += increment;
        }

        double? percent = limit is > 0 ? Math.Round(used * 100.0 / limit.Value, 1) : null;
        return new UsageResult(operation, today, used, limit, percent, allowed);
    }
}
