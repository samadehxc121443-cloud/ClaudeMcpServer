namespace ClaudeMcpServer.LicenseServer.Models;

/// <summary>A license key issued to a client, with plan, validity window and revocation state.</summary>
public sealed class LicenseKey
{
    /// <summary>Database identity.</summary>
    public int Id { get; set; }

    /// <summary>The API key string the client presents (unique).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Display name of the client the key belongs to.</summary>
    public string ClientName { get; set; } = string.Empty;

    /// <summary>Optional free-form notes about the key.</summary>
    public string? Notes { get; set; }

    /// <summary>Optional plan name (e.g. Free, Pro). Denormalized from <see cref="Plan"/> when a PlanId is set.</summary>
    public string? PlanName { get; set; }

    /// <summary>Optional reference to the plan this key was created on.</summary>
    public int? PlanId { get; set; }

    /// <summary>Navigation to the plan, when <see cref="PlanId"/> is set.</summary>
    public Plan? Plan { get; set; }

    /// <summary>False when the key has been revoked.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the key was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>UTC expiry; null means the key never expires.</summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>UTC timestamp of the last successful validation, if any.</summary>
    public DateTime? LastValidatedAt { get; set; }
}
