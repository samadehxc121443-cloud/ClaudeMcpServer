using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>Data access for <see cref="LicenseKey"/> entities.</summary>
public interface ILicenseKeyRepository
{
    /// <summary>Finds a key by database identity, or null.</summary>
    /// <param name="id">Database identity.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<LicenseKey?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Finds a key by its key string, or null.</summary>
    /// <param name="key">The license key string.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<LicenseKey?> GetByKeyAsync(string key, CancellationToken ct = default);

    /// <summary>Returns all keys, newest first.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<LicenseKey>> GetAllAsync(CancellationToken ct = default);

    /// <summary>Stages a new key for insertion.</summary>
    /// <param name="entity">The key to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(LicenseKey entity, CancellationToken ct = default);
}
