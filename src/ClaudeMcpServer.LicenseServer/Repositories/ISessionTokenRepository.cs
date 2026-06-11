using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>Data access for <see cref="SessionToken"/> entities.</summary>
public interface ISessionTokenRepository
{
    /// <summary>Stages a new session token for insertion.</summary>
    /// <param name="token">The token to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(SessionToken token, CancellationToken ct = default);

    /// <summary>Stages removal of all expired tokens belonging to a client.</summary>
    /// <param name="clientName">The client whose expired tokens are removed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task RemoveExpiredForClientAsync(string clientName, CancellationToken ct = default);
}
