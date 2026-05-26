using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Factories;

public sealed class LicenseKeyFactory : ILicenseKeyFactory
{
    public LicenseKey Create(CreateKeyRequest req, DateTime? expiresAt) => new()
    {
        Key = Guid.NewGuid().ToString("N"),
        ClientName = req.ClientName.Trim(),
        Notes = req.Notes?.Trim(),
        PlanName = req.PlanName?.Trim(),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = expiresAt
    };
}
