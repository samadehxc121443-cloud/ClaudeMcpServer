using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Repositories;

public interface ILicenseKeyRepository
{
    Task<LicenseKey?> GetByIdAsync(int id, CancellationToken ct = default);
    Task<LicenseKey?> GetByKeyAsync(string key, CancellationToken ct = default);
    Task<IReadOnlyList<LicenseKey>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(LicenseKey entity, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
