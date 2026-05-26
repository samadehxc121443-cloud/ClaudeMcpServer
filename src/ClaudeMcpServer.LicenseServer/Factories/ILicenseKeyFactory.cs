using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Factories;

public interface ILicenseKeyFactory
{
    LicenseKey Create(CreateKeyRequest req, DateTime? expiresAt);
}
