using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Models;
using ClaudeMcpServer.LicenseServer.Repositories;

namespace ClaudeMcpServer.LicenseServer.Services;

public sealed class LicenseManagerService(
    ILicenseKeyRepository licenseRepo,
    ISessionTokenRepository tokenRepo) : ILicenseManagerService
{
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
        await licenseRepo.SaveChangesAsync(ct);

        return new ValidateResult(true, entry.ClientName, null);
    }

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
        await licenseRepo.SaveChangesAsync(ct);

        return new TokenResult(session.Token, session.ClientName, session.ExpiresAt);
    }

    public async Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default)
    {
        var keys = await licenseRepo.GetAllAsync(ct);
        return keys.Select(k => new KeySummary(
            k.Id, k.Key, k.ClientName, k.Notes, k.PlanName,
            k.IsActive, k.CreatedAt, k.ExpiresAt, k.LastValidatedAt))
            .ToList();
    }

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
        await licenseRepo.SaveChangesAsync(ct);

        return new CreateKeyResult(
            entry.Id, entry.Key, entry.ClientName, entry.Notes, entry.PlanName,
            entry.IsActive, entry.CreatedAt, entry.ExpiresAt);
    }

    public async Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default)
    {
        var entry = await licenseRepo.GetByIdAsync(id, ct);
        if (entry is null) return null;

        entry.IsActive = false;
        await licenseRepo.SaveChangesAsync(ct);

        return new RevokeResult(true, entry.Id, entry.ClientName);
    }

    public async Task<int> CountKeysAsync(CancellationToken ct = default)
    {
        var keys = await licenseRepo.GetAllAsync(ct);
        return keys.Count;
    }
}
