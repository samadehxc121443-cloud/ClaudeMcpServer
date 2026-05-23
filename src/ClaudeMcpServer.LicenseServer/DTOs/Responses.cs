namespace ClaudeMcpServer.LicenseServer.DTOs;

public record ValidateResult(bool IsValid, string? ClientName, string? Message);

public record TokenResult(string Token, string ClientName, DateTime ExpiresAt);

public record KeySummary(
    int Id, string Key, string ClientName, string? Notes, string? PlanName,
    bool IsActive, DateTime CreatedAt, DateTime? ExpiresAt, DateTime? LastValidatedAt);

public record CreateKeyResult(
    int Id, string Key, string ClientName, string? Notes, string? PlanName,
    bool IsActive, DateTime CreatedAt, DateTime? ExpiresAt);

public record RevokeResult(bool Revoked, int Id, string ClientName);
