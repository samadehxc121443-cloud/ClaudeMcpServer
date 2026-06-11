using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>EF Core implementation of <see cref="ILicenseKeyRepository"/>.</summary>
/// <param name="db">The database context.</param>
public sealed class LicenseKeyRepository(LicenseDbContext db) : ILicenseKeyRepository
{
    /// <inheritdoc />
    public Task<LicenseKey?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.LicenseKeys.FindAsync([id], ct).AsTask();

    /// <inheritdoc />
    public Task<LicenseKey?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key, ct);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LicenseKey>> GetAllAsync(CancellationToken ct = default) =>
        await db.LicenseKeys
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    /// <inheritdoc />
    public async Task AddAsync(LicenseKey entity, CancellationToken ct = default) =>
        await db.LicenseKeys.AddAsync(entity, ct);
}
