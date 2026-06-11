using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Services;
using ClaudeMcpServer.LicenseServer.Tests.Fakes;
using Microsoft.Extensions.Logging.Abstractions;

namespace ClaudeMcpServer.LicenseServer.Tests.Services;

/// <summary>Tests for the <see cref="LoggingLicenseManagerService"/> decorator: it must delegate every call unchanged.</summary>
public class LoggingLicenseManagerServiceTests
{
    /// <summary>Builds the logging decorator around a counting fake.</summary>
    private static (LoggingLicenseManagerService Service, FakeLicenseManagerService Inner) CreateService()
    {
        var inner = new FakeLicenseManagerService();
        var service = new LoggingLicenseManagerService(
            inner, NullLogger<LoggingLicenseManagerService>.Instance);
        return (service, inner);
    }

    /// <summary>Validation results pass through unchanged (valid case).</summary>
    [Fact]
    public async Task ValidateAsync_Passes_Through_Valid_Result()
    {
        var (service, inner) = CreateService();
        inner.ValidateResult = new ValidateResult(true, "Client X", null);

        var result = await service.ValidateAsync("some-api-key");

        Assert.True(result.IsValid);
        Assert.Equal("Client X", result.ClientName);
        Assert.Equal(1, inner.ValidateCalls);
    }

    /// <summary>Validation results pass through unchanged (invalid case, which logs a warning).</summary>
    [Fact]
    public async Task ValidateAsync_Passes_Through_Invalid_Result()
    {
        var (service, inner) = CreateService();
        inner.ValidateResult = new ValidateResult(false, "Client X", "License key has been revoked.");

        var result = await service.ValidateAsync("some-api-key");

        Assert.False(result.IsValid);
        Assert.Equal("License key has been revoked.", result.Message);
    }

    /// <summary>Token exchange delegates to the inner service.</summary>
    [Fact]
    public async Task ExchangeTokenAsync_Delegates_To_Inner()
    {
        var (service, inner) = CreateService();

        var result = await service.ExchangeTokenAsync("some-api-key");

        Assert.Equal("fake-token", result.Token);
        Assert.Equal(1, inner.ExchangeCalls);
    }

    /// <summary>Admin key checks delegate to the inner service.</summary>
    [Fact]
    public async Task IsAdminKeyValidAsync_Delegates_To_Inner()
    {
        var (service, inner) = CreateService();

        Assert.True(await service.IsAdminKeyValidAsync("admin-key"));
        Assert.Equal(1, inner.AdminKeyCalls);
    }
}
