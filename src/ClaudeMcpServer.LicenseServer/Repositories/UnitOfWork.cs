using ClaudeMcpServer.LicenseServer.Data;

namespace ClaudeMcpServer.LicenseServer.Repositories;

public sealed class UnitOfWork(LicenseDbContext db) : IUnitOfWork
{
    public Task<int> CommitAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
