using ClaudeMcpServer.LicenseServer.Models;

namespace ClaudeMcpServer.LicenseServer.Specifications;

public sealed class LicenseKeyNotExpiredSpecification : ISpecification<LicenseKey>
{
    public bool IsSatisfiedBy(LicenseKey candidate) =>
        !candidate.ExpiresAt.HasValue || candidate.ExpiresAt.Value >= DateTime.UtcNow;

    public string GetFailureMessage(LicenseKey candidate) =>
        $"License expired on {candidate.ExpiresAt!.Value:yyyy-MM-dd}.";
}
