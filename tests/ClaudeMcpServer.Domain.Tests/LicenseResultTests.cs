using ClaudeMcpServer.Domain.Interfaces;

namespace ClaudeMcpServer.Domain.Tests;

/// <summary>Tests for the <see cref="LicenseResult"/> value object.</summary>
public class LicenseResultTests
{
    /// <summary>Valid factory sets IsValid true and preserves the client name.</summary>
    [Fact]
    public void Valid_Sets_IsValid_True_And_ClientName()
    {
        var result = LicenseResult.Valid("Acme Corp");

        Assert.True(result.IsValid);
        Assert.Equal("Acme Corp", result.ClientName);
        Assert.Contains("Acme Corp", result.Message);
    }

    /// <summary>Invalid factory sets IsValid false, empty client name, and preserves the reason.</summary>
    [Fact]
    public void Invalid_Sets_IsValid_False_Empty_ClientName_And_Reason_Message()
    {
        var result = LicenseResult.Invalid("Key has been revoked.");

        Assert.False(result.IsValid);
        Assert.Equal(string.Empty, result.ClientName);
        Assert.Equal("Key has been revoked.", result.Message);
    }

    /// <summary>DevMode factory produces a valid result with the sentinel "dev" client name.</summary>
    [Fact]
    public void DevMode_Sets_IsValid_True_With_Dev_ClientName()
    {
        var result = LicenseResult.DevMode();

        Assert.True(result.IsValid);
        Assert.Equal("dev", result.ClientName);
        Assert.NotEmpty(result.Message);
    }

    /// <summary>Two Valid results with the same client name are equal (record value semantics).</summary>
    [Fact]
    public void Valid_Results_With_Same_ClientName_Are_Equal()
    {
        var a = LicenseResult.Valid("Jorge Lopez");
        var b = LicenseResult.Valid("Jorge Lopez");

        Assert.Equal(a, b);
    }

    /// <summary>Valid and Invalid results are never equal regardless of content.</summary>
    [Fact]
    public void Valid_And_Invalid_Results_Are_Not_Equal()
    {
        Assert.NotEqual(LicenseResult.Valid("x"), LicenseResult.Invalid("x"));
    }
}
