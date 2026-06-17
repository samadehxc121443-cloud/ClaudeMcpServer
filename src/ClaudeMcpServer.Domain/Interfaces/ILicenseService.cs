namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Validates the server's API key against a remote license server.
/// Allows the MCP server to remain stdio-based while enforcing per-client
/// licensing through an outbound HTTP call on startup.
/// </summary>
public interface ILicenseService
{
    /// <summary>
    /// Validates the configured API key against the remote license server.
    /// Returns a successful result with the client display name when the key is valid.
    /// </summary>
    Task<LicenseResult> ValidateAsync(CancellationToken ct);

    /// <summary>
    /// Checks whether <paramref name="count"/> more units of <paramref name="operation"/>
    /// would fit within today's plan limit, without consuming any quota.
    /// Fails open: when tracking is unavailable (dev mode, unreachable server) the
    /// operation is allowed so a licensing-server outage never blocks core features.
    /// </summary>
    /// <param name="operation">The metered operation, e.g. "email".</param>
    /// <param name="count">How many units the caller intends to consume.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UsageStatus> CheckUsageAsync(string operation, int count, CancellationToken ct);

    /// <summary>
    /// Records <paramref name="count"/> units of <paramref name="operation"/> against
    /// today's counter, after the work succeeded. Best-effort: failures are logged,
    /// not thrown, so a tracking error never fails an operation that already happened.
    /// </summary>
    /// <param name="operation">The metered operation, e.g. "email".</param>
    /// <param name="count">How many units were consumed.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<UsageStatus> RecordUsageAsync(string operation, int count, CancellationToken ct);
}

/// <summary>Snapshot of usage for a metered operation against the plan's daily limit.</summary>
/// <param name="Allowed">Whether the requested/recorded usage fits the limit.</param>
/// <param name="Used">Units consumed today.</param>
/// <param name="Limit">Daily limit from the plan; null means unlimited.</param>
/// <param name="PercentUsed">Used/Limit as a percentage; null when unlimited.</param>
/// <param name="Tracked">False when tracking is off (dev mode) or the server was unreachable.</param>
public sealed record UsageStatus(bool Allowed, int Used, int? Limit, double? PercentUsed, bool Tracked)
{
    /// <summary>Result used when tracking is unavailable: allowed, but not counted.</summary>
    public static UsageStatus Untracked() => new(true, 0, null, null, false);
}

/// <summary>Result of a license validation check.</summary>
/// <param name="IsValid">Whether the API key is valid and the license is active.</param>
/// <param name="ClientName">Display name of the licensed client, when valid.</param>
/// <param name="Message">Human-readable message from the license server.</param>
public sealed record LicenseResult(bool IsValid, string ClientName, string Message)
{
    /// <summary>Creates a successful license result.</summary>
    public static LicenseResult Valid(string clientName) =>
        new(true, clientName, $"License valid for {clientName}.");

    /// <summary>Creates a failed license result.</summary>
    public static LicenseResult Invalid(string reason) =>
        new(false, string.Empty, reason);

    /// <summary>Creates a skipped result used when no license server is configured (dev mode).</summary>
    public static LicenseResult DevMode() =>
        new(true, "dev", "No license server configured — running in dev mode.");
}
