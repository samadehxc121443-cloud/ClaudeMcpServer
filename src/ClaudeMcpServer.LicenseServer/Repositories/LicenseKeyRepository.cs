using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

public sealed class LicenseKeyRepository(LicenseDbContext db) : ILicenseKeyRepository
{
    public Task<LicenseKey?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.LicenseKeys.FindAsync([id], ct).AsTask();

    public Task<LicenseKey?> GetByKeyAsync(string key, CancellationToken ct = default) =>
        db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == key, ct);

    public async Task<IReadOnlyList<LicenseKey>> GetAllAsync(CancellationToken ct = default) =>
        await db.LicenseKeys
            .OrderByDescending(k => k.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(LicenseKey entity, CancellationToken ct = default) =>
        await db.LicenseKeys.AddAsync(entity, ct);
}
