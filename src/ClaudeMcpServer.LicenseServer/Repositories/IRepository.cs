namespace ClaudeMcpServer.LicenseServer.Repositories;

public interface IRepository<T> where T : class
{
    Task AddAsync(T entity, CancellationToken ct = default);
}
