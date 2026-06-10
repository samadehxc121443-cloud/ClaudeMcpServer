    using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Repositories;

public interface ISessionTokenRepository
{
    Task AddAsync(SessionToken token, CancellationToken ct = default);
    Task RemoveExpiredForClientAsync(string clientName, CancellationToken ct = default);
}
