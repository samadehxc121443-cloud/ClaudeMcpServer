using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>EF Core implementation of <see cref="IPlanRepository"/>.</summary>
/// <param name="db">The database context.</param>
public sealed class PlanRepository(LicenseDbContext db) : IPlanRepository
{
    /// <inheritdoc />
    public Task<Plan?> GetByIdAsync(int id, CancellationToken ct = default) =>
        db.Plans.FindAsync([id], ct).AsTask();

    /// <inheritdoc />
    public async Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken ct = default) =>
        await db.Plans
            .Where(p => p.IsActive)
            .OrderBy(p => p.Price)
            .ToListAsync(ct);

    /// <inheritdoc />
    public Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default) =>
        db.Plans.AnyAsync(p => p.Name == name && p.IsActive, ct);

    /// <inheritdoc />
    public async Task AddAsync(Plan entity, CancellationToken ct = default) =>
        await db.Plans.AddAsync(entity, ct);
}
