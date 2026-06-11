namespace ClaudeMcpServer.LicenseServer.Models;

/// <summary>
/// An administrative access key. Admin keys are data, not configuration:
/// they live in the database and can be rotated or revoked at runtime.
/// </summary>
public sealed class AdminKey
{
    /// <summary>Database identity.</summary>
    public int Id { get; set; }

    /// <summary>The admin key string presented in the X-Admin-Key header (unique).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Human-readable label for the key (e.g. "bootstrap", owner name).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>False when the key has been revoked.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the key was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
