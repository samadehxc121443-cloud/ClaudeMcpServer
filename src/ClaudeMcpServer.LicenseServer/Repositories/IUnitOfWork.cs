namespace ClaudeMcpServer.LicenseServer.Repositories;

public interface IUnitOfWork
{
    Task<int> CommitAsync(CancellationToken ct = default);
}
