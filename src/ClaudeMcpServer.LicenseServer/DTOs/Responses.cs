namespace ClaudeMcpServer.LicenseServer.DTOs;

/// <summary>Outcome of a license validation.</summary>
/// <param name="IsValid">True when the key exists, is active and not expired.</param>
/// <param name="ClientName">Client the key belongs to, when known.</param>
/// <param name="Message">Failure reason when <paramref name="IsValid"/> is false.</param>
public record ValidateResult(bool IsValid, string? ClientName, string? Message);

/// <summary>A session token issued in exchange for a valid license key.</summary>
/// <param name="Token">The opaque session token.</param>
/// <param name="ClientName">Client the token was issued to.</param>
/// <param name="ExpiresAt">UTC expiry of the token.</param>
public record TokenResult(string Token, string ClientName, DateTime ExpiresAt);

/// <summary>Read model of a license key for admin listings.</summary>
/// <param name="Id">Database identity.</param>
/// <param name="Key">The license key string.</param>
/// <param name="ClientName">Client the key belongs to.</param>
/// <param name="Notes">Optional notes.</param>
/// <param name="PlanName">Optional plan name.</param>
/// <param name="IsActive">False when revoked.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="ExpiresAt">UTC expiry; null means never.</param>
/// <param name="LastValidatedAt">UTC timestamp of the last successful validation.</param>
public record KeySummary(
    int Id, string Key, string ClientName, string? Notes, string? PlanName,
    bool IsActive, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastValidatedAt);

/// <summary>Result of creating a license key, including the generated key string.</summary>
/// <param name="Id">Database identity.</param>
/// <param name="Key">The generated license key string.</param>
/// <param name="ClientName">Client the key belongs to.</param>
/// <param name="Notes">Optional notes.</param>
/// <param name="PlanName">Optional plan name.</param>
/// <param name="IsActive">Always true on creation.</param>
/// <param name="CreatedAt">UTC creation timestamp.</param>
/// <param name="ExpiresAt">UTC expiry; null means never.</param>
public record CreateKeyResult(
    int Id, string Key, string ClientName, string? Notes, string? PlanName,
    bool IsActive, DateTime CreatedAt, DateTime? ExpiresAt);

/// <summary>Result of revoking a license key.</summary>
/// <param name="Revoked">True when the key was deactivated.</param>
/// <param name="Id">Database identity of the revoked key.</param>
/// <param name="ClientName">Client the key belonged to.</param>
public record RevokeResult(bool Revoked, int Id, string ClientName);

/// <summary>
/// Usage snapshot for a key/operation on the current UTC date. Designed so the
/// MCP server can decide in ONE round-trip: block when <paramref name="Allowed"/>
/// is false, warn the client when <paramref name="PercentUsed"/> reaches 90.
/// </summary>
/// <param name="Operation">The metered operation.</param>
/// <param name="Date">UTC date of the counter.</param>
/// <param name="Used">Units consumed today (after this report, when reporting).</param>
/// <param name="Limit">Daily limit from the key's plan; null means unlimited.</param>
/// <param name="PercentUsed">Used/Limit in percent; null when unlimited.</param>
/// <param name="Allowed">False when the report would exceed the limit (nothing was counted).</param>
public record UsageResult(string Operation, DateOnly Date, int Used, int? Limit, double? PercentUsed, bool Allowed);

/// <summary>Read model of a license plan.</summary>
/// <param name="Id">Database identity.</param>
/// <param name="Name">Plan name.</param>
/// <param name="Price">Monthly price; 0 for free plans.</param>
/// <param name="MaxEmailsPerDay">Daily email limit; null means unlimited.</param>
/// <param name="DurationDays">Default key validity in days; null means no expiry.</param>
/// <param name="IsActive">False when the plan is retired.</param>
public record PlanSummary(int Id, string Name, decimal Price, int? MaxEmailsPerDay, int? DurationDays, bool IsActive);
