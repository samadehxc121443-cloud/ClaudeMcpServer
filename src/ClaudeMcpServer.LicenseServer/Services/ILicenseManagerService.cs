using ClaudeMcpServer.LicenseServer.DTOs;

namespace ClaudeMcpServer.LicenseServer.Services;

/// <summary>Business operations over license keys, session tokens and admin keys.</summary>
public interface ILicenseManagerService
{
    /// <summary>Validates a license key (existence, revocation, expiry) and stamps its last-validated time.</summary>
    /// <param name="apiKey">The license key to validate.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<ValidateResult> ValidateAsync(string apiKey, CancellationToken ct = default);

    /// <summary>Exchanges a valid license key for a one-hour session token.</summary>
    /// <param name="apiKey">The license key to exchange.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="UnauthorizedAccessException">The key is unknown, revoked or expired.</exception>
    Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default);

    /// <summary>Returns a summary of every license key, newest first.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default);

    /// <summary>Creates a new license key with a generated key string.</summary>
    /// <param name="req">Creation parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<CreateKeyResult> CreateKeyAsync(CreateKeyRequest req, CancellationToken ct = default);

    /// <summary>Deactivates a license key by database identity.</summary>
    /// <param name="id">Database identity of the key.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The revocation result, or null when the key does not exist.</returns>
    Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default);

    /// <summary>Counts the stored license keys.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<int> CountKeysAsync(CancellationToken ct = default);

    /// <summary>True when the given admin key exists in the database and is active.</summary>
    /// <param name="key">The admin key string.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<bool> IsAdminKeyValidAsync(string key, CancellationToken ct = default);

    /// <summary>Returns all active plans, cheapest first.</summary>
    /// <param name="ct">Cancellation token.</param>
    Task<IReadOnlyList<PlanSummary>> GetActivePlansAsync(CancellationToken ct = default);

    /// <summary>Creates a new license plan.</summary>
    /// <param name="req">Plan parameters.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">An active plan with the same name already exists.</exception>
    Task<PlanSummary> CreatePlanAsync(CreatePlanRequest req, CancellationToken ct = default);

    /// <summary>Retires a plan: existing keys keep working, new keys can't use it.</summary>
    /// <param name="id">Database identity of the plan.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The retired plan, or null when it does not exist.</returns>
    Task<PlanSummary?> DeactivatePlanAsync(int id, CancellationToken ct = default);
}
