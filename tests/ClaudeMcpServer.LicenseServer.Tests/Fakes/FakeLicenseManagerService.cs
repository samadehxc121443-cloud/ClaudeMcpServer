using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Services;

namespace ClaudeMcpServer.LicenseServer.Tests.Fakes;

/// <summary>
/// Hand-rolled fake of <see cref="ILicenseManagerService"/> that counts calls
/// and returns canned results, used to test the decorators in isolation.
/// </summary>
public sealed class FakeLicenseManagerService : ILicenseManagerService
{
    /// <summary>Number of times <see cref="ValidateAsync"/> was invoked.</summary>
    public int ValidateCalls { get; private set; }

    /// <summary>Number of times <see cref="ExchangeTokenAsync"/> was invoked.</summary>
    public int ExchangeCalls { get; private set; }

    /// <summary>Number of times <see cref="IsAdminKeyValidAsync"/> was invoked.</summary>
    public int AdminKeyCalls { get; private set; }

    /// <summary>Canned result returned by <see cref="ValidateAsync"/>.</summary>
    public ValidateResult ValidateResult { get; set; } = new(true, "Fake Client", null);

    /// <inheritdoc />
    public Task<ValidateResult> ValidateAsync(string apiKey, CancellationToken ct = default)
    {
        ValidateCalls++;
        return Task.FromResult(ValidateResult);
    }

    /// <inheritdoc />
    public Task<TokenResult> ExchangeTokenAsync(string apiKey, CancellationToken ct = default)
    {
        ExchangeCalls++;
        return Task.FromResult(new TokenResult("fake-token", "Fake Client", DateTime.UtcNow.AddHours(1)));
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<KeySummary>> GetAllKeysAsync(CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<KeySummary>>([]);

    /// <inheritdoc />
    public Task<CreateKeyResult> CreateKeyAsync(CreateKeyRequest req, CancellationToken ct = default) =>
        Task.FromResult(new CreateKeyResult(1, "fake-key", req.ClientName, req.Notes, req.PlanName, true, DateTime.UtcNow, null));

    /// <inheritdoc />
    public Task<RevokeResult?> RevokeKeyAsync(int id, CancellationToken ct = default) =>
        Task.FromResult<RevokeResult?>(new RevokeResult(true, id, "Fake Client"));

    /// <inheritdoc />
    public Task<int> CountKeysAsync(CancellationToken ct = default) => Task.FromResult(0);

    /// <inheritdoc />
    public Task<bool> IsAdminKeyValidAsync(string key, CancellationToken ct = default)
    {
        AdminKeyCalls++;
        return Task.FromResult(true);
    }
}
