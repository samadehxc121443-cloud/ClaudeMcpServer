namespace ClaudeMcpServer.LicenseServer.DTOs;

/// <summary>Request body for license validation and token exchange.</summary>
/// <param name="ApiKey">The license key to validate or exchange.</param>
public record ValidateRequest(string ApiKey);

/// <summary>Request body for creating a new license key.</summary>
/// <param name="ClientName">Display name of the client (required).</param>
/// <param name="Notes">Optional free-form notes.</param>
/// <param name="PlanName">Optional plan name; overridden by the plan's name when <paramref name="PlanId"/> is set.</param>
/// <param name="ExpiresAt">Explicit UTC expiry; takes precedence over <paramref name="DurationDays"/> and the plan default.</param>
/// <param name="DurationDays">Validity in days from now; takes precedence over the plan default.</param>
/// <param name="PlanId">Optional plan to create the key on; expiry defaults to the plan's duration.</param>
public record CreateKeyRequest(string ClientName, string? Notes, string? PlanName, DateTime? ExpiresAt, int? DurationDays, int? PlanId = null);

/// <summary>Request body for creating a new license plan.</summary>
/// <param name="Name">Plan name (required, unique among active plans).</param>
/// <param name="Price">Monthly price; 0 for free plans.</param>
/// <param name="MaxEmailsPerDay">Daily email limit; null means unlimited.</param>
/// <param name="DurationDays">Default key validity in days; null means no expiry.</param>
public record CreatePlanRequest(string Name, decimal Price, int? MaxEmailsPerDay, int? DurationDays);

/// <summary>Request body for reporting (or querying) usage of a metered operation.</summary>
/// <param name="ApiKey">The license key the usage belongs to.</param>
/// <param name="Operation">The metered operation, e.g. "email".</param>
/// <param name="Count">How many units to report; defaults to 1. Ignored on queries.</param>
public record ReportUsageRequest(string ApiKey, string Operation, int Count = 1);
