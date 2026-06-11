namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>Data access for <see cref="Models.AdminKey"/> entities.</summary>
public interface IAdminKeyRepository
{
    /// <summary>True when an active admin key with the given key string exists.</summary>
    /// <param name="key">The admin key string to check.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> ExistsActiveAsync(string key, CancellationToken ct = default);

    /// <summary>True when at least one active admin key exists.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> AnyActiveAsync(CancellationToken ct = default);
}
