namespace ClaudeMcpServer.LicenseServer.Specifications;

public interface ISpecification<T>
{
    bool IsSatisfiedBy(T candidate);
    string GetFailureMessage(T candidate);
}
