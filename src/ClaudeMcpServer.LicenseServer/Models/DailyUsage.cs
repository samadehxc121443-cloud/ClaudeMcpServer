namespace ClaudeMcpServer.LicenseServer.Models;

/// <summary>
/// Daily usage counter for a license key and operation (e.g. "email").
/// One row per key/date/operation, incremented as the MCP server reports usage.
/// </summary>
public sealed class DailyUsage
{
    /// <summary>Database identity.</summary>
    public int Id { get; set; }

    /// <summary>The license key this counter belongs to.</summary>
    public int LicenseKeyId { get; set; }

    /// <summary>Navigation to the license key.</summary>
    public LicenseKey? LicenseKey { get; set; }

    /// <summary>UTC date the counter applies to.</summary>
    public DateOnly Date { get; set; }

    /// <summary>The metered operation, e.g. "email".</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>How many times the operation ran on this date.</summary>
    public int Count { get; set; }
}
