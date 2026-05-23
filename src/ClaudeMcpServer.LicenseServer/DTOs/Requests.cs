namespace ClaudeMcpServer.LicenseServer.DTOs;

public record ValidateRequest(string ApiKey);
public record CreateKeyRequest(string ClientName, string? Notes, string? PlanName, DateTime? ExpiresAt, int? DurationDays);
