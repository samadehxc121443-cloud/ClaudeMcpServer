using ClaudeMcpServer.LicenseServer.DTOs;

namespace ClaudeMcpServer.LicenseServer.Services;

public interface ILicenseManagerService
{
    Task<ValidateResult> ValidateAsync(string apiKey, CancellationToken ct = default);
    Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default);
    Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default);
    Task<CreateKeyResult> CreateKeyAsync(CreateKeyRequest req, CancellationToken ct = default);
    Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default);
    Task<int> CountKeysAsync(CancellationToken ct = default);
}
