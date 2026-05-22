namespace ClaudeMcpServer.LicenseServer.Models;

public sealed class SessionToken
{
    public int Id { get; set; }
    public string Token { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
}
