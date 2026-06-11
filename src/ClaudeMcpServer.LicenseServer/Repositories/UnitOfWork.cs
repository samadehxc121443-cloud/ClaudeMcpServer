using ClaudeMcpServer.LicenseServer.Data;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>EF Core implementation of <see cref="IUnitOfWork"/> over the shared context.</summary>
/// <param name="db">The database context.</param>
public sealed class UnitOfWork(LicenseDbContext db) : IUnitOfWork
{
    /// <inheritdoc />
    public Task<int> CommitAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
