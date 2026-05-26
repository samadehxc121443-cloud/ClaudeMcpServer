using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Factories;

public interface ISessionTokenFactory
{
    SessionToken Create(string clientName);
}
