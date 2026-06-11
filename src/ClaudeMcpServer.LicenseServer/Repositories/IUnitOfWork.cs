namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>
/// Unit of Work: services stage changes through repositories and persist them
/// atomically with a single commit, never touching the DbContext directly.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>Persists all staged changes in one transaction.</summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The number of affected rows.</returns>
    Task<int> CommitAsync(CancellationToken ct = default);
}
