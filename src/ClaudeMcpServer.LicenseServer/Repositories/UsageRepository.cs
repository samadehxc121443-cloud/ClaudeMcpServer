using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>EF Core implementation of <see cref="IUsageRepository"/>.</summary>
/// <param name="db">The database context.</param>
public sealed class UsageRepository(LicenseDbContext db) : IUsageRepository
{
    /// <inheritdoc />
    public Task<DailyUsage?> GetAsync(int licenseKeyId, DateOnly date, string operation, CancellationToken ct = default) =>
        db.DailyUsages.FirstOrDefaultAsync(
            u => u.LicenseKeyId == licenseKeyId && u.Date == date && u.Operation == operation, ct);

    /// <inheritdoc />
    public async Task AddAsync(DailyUsage entity, CancellationToken ct = default) =>
        await db.DailyUsages.AddAsync(entity, ct);
}
