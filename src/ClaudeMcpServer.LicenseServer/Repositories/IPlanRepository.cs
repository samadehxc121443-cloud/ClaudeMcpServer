using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>Data access for <see cref="Plan"/> entities.</summary>
public interface IPlanRepository
{
    /// <summary>Finds a plan by database identity, or null.</summary>
    /// <param name="id">Database identity.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Plan?> GetByIdAsync(int id, CancellationToken ct = default);

    /// <summary>Returns all active plans, cheapest first.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<Plan>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>True when an active plan with the given name already exists.</summary>
    /// <param name="name">Plan name to check.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);

    /// <summary>Stages a new plan for insertion.</summary>
    /// <param name="entity">The plan to add.</param>
    /// <param name="ct">Cancellation token.</param>
    Task AddAsync(Plan entity, CancellationToken ct = default);
}
