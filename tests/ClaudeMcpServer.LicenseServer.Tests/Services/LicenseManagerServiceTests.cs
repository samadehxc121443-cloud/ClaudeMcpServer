using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Models;
using ClaudeMcpServer.LicenseServer.Repositories;
using ClaudeMcpServer.LicenseServer.Services;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Tests.Services;

/// <summary>Tests for <see cref="LicenseManagerService"/> against an in-memory database with the real repositories.</summary>
public class LicenseManagerServiceTests
{
    /// <summary>Builds a service wired to a fresh in-memory database, returning the context for seeding/asserting.</summary>
    private static (LicenseManagerService Service, LicenseDbContext Db) CreateService()
    {
        var options = new DbContextOptionsBuilder<LicenseDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var db = new LicenseDbContext(options);
        var service = new LicenseManagerService(
            new LicenseKeyRepository(db),
            new SessionTokenRepository(db),
            new AdminKeyRepository(db),
            new UnitOfWork(db));
        return (service, db);
    }

    /// <summary>Inserts a license key and saves it.</summary>
    private static LicenseKey SeedKey(LicenseDbContext db, string key, bool isActive = true, DateTime? expiresAt = null)
    {
        var entity = new LicenseKey { Key = key, ClientName = "Test Client", IsActive = isActive, ExpiresAt = expiresAt };
        db.LicenseKeys.Add(entity);
        db.SaveChanges();
        return entity;
    }

    /// <summary>An unknown key must be reported as invalid with a not-found message.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_When_Key_Not_Found()
    {
        var (service, _) = CreateService();

        var result = await service.ValidateAsync("does-not-exist");

        Assert.False(result.IsValid);
        Assert.Equal("License key not found.", result.Message);
    }

    /// <summary>A revoked key must be reported as invalid.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_When_Key_Revoked()
    {
        var (service, db) = CreateService();
        SeedKey(db, "revoked-key", isActive: false);

        var result = await service.ValidateAsync("revoked-key");

        Assert.False(result.IsValid);
        Assert.Contains("revoked", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An expired key must be reported as invalid with the expiry date.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Invalid_When_Key_Expired()
    {
        var (service, db) = CreateService();
        SeedKey(db, "expired-key", expiresAt: DateTime.UtcNow.AddDays(-1));

        var result = await service.ValidateAsync("expired-key");

        Assert.False(result.IsValid);
        Assert.Contains("expired", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>A valid key passes validation and its LastValidatedAt is stamped.</summary>
    [Fact]
    public async Task ValidateAsync_Returns_Valid_And_Stamps_LastValidatedAt()
    {
        var (service, db) = CreateService();
        var entity = SeedKey(db, "good-key");

        var result = await service.ValidateAsync("good-key");

        Assert.True(result.IsValid);
        Assert.Equal("Test Client", result.ClientName);
        Assert.NotNull(entity.LastValidatedAt);
    }

    /// <summary>Token exchange must reject unknown keys.</summary>
    [Fact]
    public async Task ExchangeTokenAsync_Throws_When_Key_Not_Found()
    {
        var (service, _) = CreateService();

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ExchangeTokenAsync("does-not-exist"));
    }

    /// <summary>Token exchange must reject revoked keys.</summary>
    [Fact]
    public async Task ExchangeTokenAsync_Throws_When_Key_Revoked()
    {
        var (service, db) = CreateService();
        SeedKey(db, "revoked-key", isActive: false);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.ExchangeTokenAsync("revoked-key"));
    }

    /// <summary>A successful exchange issues a session token that expires in one hour.</summary>
    [Fact]
    public async Task ExchangeTokenAsync_Issues_Token_With_One_Hour_Expiry()
    {
        var (service, db) = CreateService();
        SeedKey(db, "good-key");

        var result = await service.ExchangeTokenAsync("good-key");

        Assert.False(string.IsNullOrWhiteSpace(result.Token));
        Assert.Equal("Test Client", result.ClientName);
        Assert.InRange(result.ExpiresAt, DateTime.UtcNow.AddMinutes(59), DateTime.UtcNow.AddMinutes(61));
        Assert.Single(db.SessionTokens);
    }

    /// <summary>Creating a key with DurationDays computes the expiry from now.</summary>
    [Fact]
    public async Task CreateKeyAsync_Computes_Expiry_From_DurationDays()
    {
        var (service, _) = CreateService();

        var result = await service.CreateKeyAsync(new CreateKeyRequest("Client A", null, "Pro", null, 30));

        Assert.NotNull(result.ExpiresAt);
        Assert.InRange(result.ExpiresAt!.Value, DateTime.UtcNow.AddDays(29), DateTime.UtcNow.AddDays(31));
    }

    /// <summary>Created keys are 32-char GUID strings and persisted as active.</summary>
    [Fact]
    public async Task CreateKeyAsync_Persists_Active_Key()
    {
        var (service, db) = CreateService();

        var result = await service.CreateKeyAsync(new CreateKeyRequest("Client A", "note", "Free", null, null));

        Assert.Equal(32, result.Key.Length);
        Assert.True(result.IsActive);
        Assert.Single(db.LicenseKeys);
    }

    /// <summary>Revoking a missing key returns null.</summary>
    [Fact]
    public async Task RevokeKeyAsync_Returns_Null_When_Missing()
    {
        var (service, _) = CreateService();

        Assert.Null(await service.RevokeKeyAsync(999));
    }

    /// <summary>Revoking an existing key deactivates it.</summary>
    [Fact]
    public async Task RevokeKeyAsync_Deactivates_Key()
    {
        var (service, db) = CreateService();
        var entity = SeedKey(db, "to-revoke");

        var result = await service.RevokeKeyAsync(entity.Id);

        Assert.NotNull(result);
        Assert.True(result!.Revoked);
        Assert.False(entity.IsActive);
    }

    /// <summary>CountKeysAsync reflects the number of stored keys.</summary>
    [Fact]
    public async Task CountKeysAsync_Returns_Number_Of_Keys()
    {
        var (service, db) = CreateService();
        SeedKey(db, "k1");
        SeedKey(db, "k2");

        Assert.Equal(2, await service.CountKeysAsync());
    }

    /// <summary>GetAllKeysAsync maps every stored key to a summary.</summary>
    [Fact]
    public async Task GetAllKeysAsync_Returns_Summaries()
    {
        var (service, db) = CreateService();
        SeedKey(db, "k1");

        var keys = await service.GetAllKeysAsync();

        var summary = Assert.Single(keys);
        Assert.Equal("k1", summary.Key);
        Assert.Equal("Test Client", summary.ClientName);
    }

    /// <summary>An active admin key stored in the database validates successfully.</summary>
    [Fact]
    public async Task IsAdminKeyValidAsync_True_For_Active_Key()
    {
        var (service, db) = CreateService();
        db.AdminKeys.Add(new AdminKey { Key = "admin-key", Name = "Test Admin" });
        db.SaveChanges();

        Assert.True(await service.IsAdminKeyValidAsync("admin-key"));
    }

    /// <summary>Inactive or unknown admin keys are rejected.</summary>
    [Fact]
    public async Task IsAdminKeyValidAsync_False_For_Inactive_Or_Missing_Key()
    {
        var (service, db) = CreateService();
        db.AdminKeys.Add(new AdminKey { Key = "inactive-key", Name = "Old Admin", IsActive = false });
        db.SaveChanges();

        Assert.False(await service.IsAdminKeyValidAsync("inactive-key"));
        Assert.False(await service.IsAdminKeyValidAsync("never-existed"));
    }
}
