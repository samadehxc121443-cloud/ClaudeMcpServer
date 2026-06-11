namespace ClaudeMcpServer.LicenseServer.Models;

/// <summary>
/// A license plan. Limits are parametrization data and live in the database,
/// never in configuration — they can be tuned at runtime from the admin UI.
/// </summary>
public sealed class Plan
{
    /// <summary>Database identity.</summary>
    public int Id { get; set; }

    /// <summary>Plan name (unique), e.g. Free, Pro.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Monthly price; 0 for free plans.</summary>
    public decimal Price { get; set; }

    /// <summary>Daily email-sending limit for keys on this plan; null means unlimited.</summary>
    public int? MaxEmailsPerDay { get; set; }

    /// <summary>Default validity in days for keys created on this plan; null means no expiry.</summary>
    public int? DurationDays { get; set; }

    /// <summary>False when the plan is retired (existing keys keep working; new keys can't use it).</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>UTC timestamp when the plan was created.</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
