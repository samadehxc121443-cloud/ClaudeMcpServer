using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Models;
using ClaudeMcpServer.LicenseServer.Repositories;

namespace ClaudeMcpServer.LicenseServer.Services;

/// <summary>Core implementation of <see cref="ILicenseManagerService"/> backed by the repositories.</summary>
/// <param name="licenseRepo">License key data access.</param>
/// <param name="tokenRepo">Session token data access.</param>
/// <param name="adminRepo">Admin key data access.</param>
/// <param name="uow">Unit of work used to persist staged changes.</param>
public sealed class LicenseManagerService(
    ILicenseKeyRepository licenseRepo,
    ISessionTokenRepository tokenRepo,
    IAdminKeyRepository adminRepo,
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
        DateTime? expiresAt = req.ExpiresAt
            ?? (req.DurationDays.HasValue ? DateTime.UtcNow.AddDays(req.DurationDays.Value) : null);

        var entry = new LicenseKey
        {
            Key = Guid.NewGuid().ToString("N"),
            ClientName = req.ClientName.Trim(),
            Notes = req.Notes?.Trim(),
            PlanName = req.PlanName?.Trim(),
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
}
