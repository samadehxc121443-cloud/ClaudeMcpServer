namespace ClaudeMcpServer.LicenseServer.Models;

public sealed class LicenseKey
{
    public int Id { get; set; }
    public string Key { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastValidatedAt { get; set; }
}
