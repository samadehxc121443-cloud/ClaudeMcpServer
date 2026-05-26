using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Models;
using ClaudeMcpServer.LicenseServer.Repositories;
using ClaudeMcpServer.LicenseServer.Specifications;

namespace ClaudeMcpServer.LicenseServer.Services;

public sealed class LicenseManagerService(
    ILicenseKeyRepository licenseRepo,
    ISessionTokenRepository tokenRepo,
    IUnitOfWork uow) : ILicenseManagerService
{
    private static readonly ISpecification<LicenseKey>[] _validationRules =
    [
        new LicenseKeyActiveSpecification(),
        new LicenseKeyNotExpiredSpecification()
    ];

    public async Task<ValidateResult> ValidateAsync(string apiKey, CancellationToken ct = default)
    {
        var entry = await licenseRepo.GetByKeyAsync(apiKey, ct);

        if (entry is null)
            return new ValidateResult(false, null, "License key not found.");

        foreach (var rule in _validationRules)
        {
            if (!rule.IsSatisfiedBy(entry))
                return new ValidateResult(false, entry.ClientName, rule.GetFailureMessage(entry));
        }

        entry.LastValidatedAt = DateTime.UtcNow;
        await uow.CommitAsync(ct);

        return new ValidateResult(true, entry.ClientName, null);
    }

    public async Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default)
    {
        var entry = await licenseRepo.GetByKeyAsync(apiKey, ct);

        if (entry is null)
            throw new UnauthorizedAccessException("License key not found.");

        foreach (var rule in _validationRules)
        {
            if (!rule.IsSatisfiedBy(entry))
                throw new UnauthorizedAccessException(rule.GetFailureMessage(entry));
        }

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
        await uow.CommitAsync(ct);

        return new CreateKeyResult(
            entry.Id, entry.Key, entry.ClientName, entry.Notes, entry.PlanName,
            entry.IsActive, entry.CreatedAt, entry.ExpiresAt);
    }

    public async Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default)
    {
        var entry = await licenseRepo.GetByIdAsync(id, ct);
        if (entry is null) return null;

        entry.IsActive = false;
        await uow.CommitAsync(ct);

        return new RevokeResult(true, entry.Id, entry.ClientName);
    }

    public async Task<int> CountKeysAsync(CancellationToken ct = default)
    {
        var keys = await licenseRepo.GetAllAsync(ct);
        return keys.Count;
    }
}
