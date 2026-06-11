using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>Data access for <see cref="DailyUsage"/> counters.</summary>
public interface IUsageRepository
{
    /// <summary>Finds the counter for a key/date/operation, or null when nothing was reported yet.</summary>
    /// <param name="licenseKeyId">Database identity of the license key.</param>
    /// <param name="date">UTC date.</param>
    /// <param name="operation">The metered operation, e.g. "email".</param>
    /// <param name="ct">Cancellation token.</param>
    Task<DailyUsage?> GetAsync(int licenseKeyId, DateOnly date, string operation, CancellationToken ct = default);

    /// <summary>Stages a new counter for insertion.</summary>
    /// <param name="entity">The counter to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(DailyUsage entity, CancellationToken ct = default);
}
