using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Factories;

public sealed class SessionTokenFactory : ISessionTokenFactory
{
    public SessionToken Create(string clientName) => new()
    {
        Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        ClientName = clientName,
        IssuedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(1)
    };
}
