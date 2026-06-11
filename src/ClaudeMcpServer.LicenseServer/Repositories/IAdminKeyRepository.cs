namespace ClaudeMcpServer.LicenseServer.Repositories;

public interface IAdminKeyRepository
{
    Task<bool> ExistsActiveAsync(string key, CancellationToken ct = default);
    Task<bool> AnyActiveAsync(CancellationToken ct = default);
}
