using ClaudeMcpServer.LicenseServer.Services;
using ClaudeMcpServer.LicenseServer.Tests.Fakes;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.LicenseServer.Tests.Services;

/// <summary>Tests for the <see cref="CachingLicenseManagerService"/> decorator using an in-memory distributed cache.</summary>
public class CachingLicenseManagerServiceTests
{
    /// <summary>Builds the caching decorator around a counting fake.</summary>
    private static (CachingLicenseManagerService Service, FakeLicenseManagerService Inner) CreateService()
    {
        var inner = new FakeLicenseManagerService();
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var service = new CachingLicenseManagerService(
            inner, cache, NullLogger<CachingLicenseManagerService>.Instance);
        return (service, inner);
    }

    /// <summary>The first validation hits the inner service; the second is served from cache.</summary>
    [Fact]
    public async Task ValidateAsync_Second_Call_Is_Served_From_Cache()
    {
        var (service, inner) = CreateService();

        var first = await service.ValidateAsync("some-api-key");
        var second = await service.ValidateAsync("some-api-key");

        Assert.Equal(1, inner.ValidateCalls);
        Assert.Equal(first.IsValid, second.IsValid);
        Assert.Equal(first.ClientName, second.ClientName);
    }

    /// <summary>Different keys are cached independently, each hitting the inner service once.</summary>
    [Fact]
    public async Task ValidateAsync_Caches_Per_Key()
    {
        var (service, inner) = CreateService();

        await service.ValidateAsync("key-one1");
        await service.ValidateAsync("key-two2");
        await service.ValidateAsync("key-one1");

        Assert.Equal(2, inner.ValidateCalls);
    }

    /// <summary>Token exchange creates state and must never be cached.</summary>
    [Fact]
    public async Task ExchangeTokenAsync_Always_Calls_Inner()
    {
        var (service, inner) = CreateService();

        await service.ExchangeTokenAsync("some-api-key");
        await service.ExchangeTokenAsync("some-api-key");

        Assert.Equal(2, inner.ExchangeCalls);
    }

    /// <summary>Admin key checks must never be cached so revocation takes effect immediately.</summary>
    [Fact]
    public async Task IsAdminKeyValidAsync_Always_Calls_Inner()
    {
        var (service, inner) = CreateService();

        await service.IsAdminKeyValidAsync("admin-key");
        await service.IsAdminKeyValidAsync("admin-key");

        Assert.Equal(2, inner.AdminKeyCalls);
    }
}
