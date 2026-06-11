namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>Common contract shared by entity repositories.</summary>
/// <typeparam name="T">The entity type the repository manages.</typeparam>
public interface IRepository<T> where T : class
{
    /// <summary>Stages a new entity for insertion (persisted on <see cref="IUnitOfWork.CommitAsync"/>).</summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(T entity, CancellationToken ct = default);
}
