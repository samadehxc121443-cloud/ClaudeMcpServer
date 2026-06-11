namespace ClaudeMcpServer.LicenseServer.DTOs;

/// <summary>Request body for license validation and token exchange.</summary>
/// <param name="ApiKey">The license key to validate or exchange.</param>
public record ValidateRequest(string ApiKey);

/// <summary>Request body for creating a new license key.</summary>
/// <param name="ClientName">Display name of the client (required).</param>
/// <param name="Notes">Optional free-form notes.</param>
/// <param name="PlanName">Optional plan name (e.g. Free, Pro).</param>
/// <param name="ExpiresAt">Explicit UTC expiry; takes precedence over <paramref name="DurationDays"/>.</param>
/// <param name="DurationDays">Validity in days from now, used when <paramref name="ExpiresAt"/> is null.</param>
public record CreateKeyRequest(string ClientName, string? Notes, string? PlanName, DateTime? ExpiresAt, int? DurationDays);
