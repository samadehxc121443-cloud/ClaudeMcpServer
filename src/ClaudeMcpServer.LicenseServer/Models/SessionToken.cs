namespace ClaudeMcpServer.LicenseServer.Models;

/// <summary>A short-lived session token issued in exchange for a valid license key.</summary>
public sealed class SessionToken
{
    /// <summary>Database identity.</summary>
    public int Id { get; set; }

    /// <summary>The opaque token string (unique).</summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>Client the token was issued to.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>UTC timestamp when the token was issued.</summary>
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC expiry of the token.</summary>
    public DateTime ExpiresAt { get; set; }
}
