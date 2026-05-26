using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Specifications;

public sealed class LicenseKeyActiveSpecification : ISpecification<LicenseKey>
{
    public bool IsSatisfiedBy(LicenseKey candidate) => candidate.IsActive;

    public string GetFailureMessage(LicenseKey candidate) => "License key has been revoked.";
}
