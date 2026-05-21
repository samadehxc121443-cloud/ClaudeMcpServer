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
