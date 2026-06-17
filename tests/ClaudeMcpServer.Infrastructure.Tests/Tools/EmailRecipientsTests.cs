using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

/// <summary>Tests for <see cref="EmailRecipients"/> parsing and the iCloud recipient cap.</summary>
public class EmailRecipientsTests
{
    private static JsonElement Json(object value) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    /// <summary>A single string in "to" yields one recipient (backward compatible).</summary>
    [Fact]
    public void TryParse_Accepts_Single_String_To()
    {
        var ok = EmailRecipients.TryParse(Json(new { to = "a@x.com" }), out var r, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(["a@x.com"], r.To);
        Assert.Equal(1, r.Total);
    }

    /// <summary>An array in "to" yields multiple recipients (mass send).</summary>
    [Fact]
    public void TryParse_Accepts_Array_To()
    {
        var ok = EmailRecipients.TryParse(Json(new { to = new[] { "a@x.com", "b@y.com" } }), out var r, out _);

        Assert.True(ok);
        Assert.Equal(2, r.To.Count);
        Assert.Equal(2, r.Total);
    }

    /// <summary>Cc and Bcc are parsed into their own lists and counted in the total.</summary>
    [Fact]
    public void TryParse_Parses_Cc_And_Bcc()
    {
        var ok = EmailRecipients.TryParse(
            Json(new { to = "a@x.com", cc = new[] { "c@x.com" }, bcc = new[] { "d@x.com", "e@x.com" } }),
            out var r, out _);

        Assert.True(ok);
        Assert.Equal(["c@x.com"], r.Cc);
        Assert.Equal(["d@x.com", "e@x.com"], r.Bcc);
        Assert.Equal(4, r.Total);
    }

    /// <summary>Blank entries are trimmed out.</summary>
    [Fact]
    public void TryParse_Skips_Blank_Entries()
    {
        var ok = EmailRecipients.TryParse(Json(new { to = new[] { "a@x.com", "  ", "" } }), out var r, out _);

        Assert.True(ok);
        Assert.Single(r.To);
    }

    /// <summary>Missing or empty "to" is rejected.</summary>
    [Fact]
    public void TryParse_Rejects_Missing_To()
    {
        var ok = EmailRecipients.TryParse(Json(new { cc = new[] { "c@x.com" } }), out _, out var error);

        Assert.False(ok);
        Assert.Contains("to", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Exactly 500 recipients is allowed (the iCloud boundary).</summary>
    [Fact]
    public void TryParse_Allows_Exactly_500()
    {
        var addrs = Enumerable.Range(0, 500).Select(i => $"u{i}@x.com").ToArray();

        var ok = EmailRecipients.TryParse(Json(new { to = addrs }), out var r, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(500, r.Total);
    }

    /// <summary>501 recipients across to+cc+bcc exceeds the iCloud cap and is rejected.</summary>
    [Fact]
    public void TryParse_Rejects_Over_500_Across_All_Fields()
    {
        var to = Enumerable.Range(0, 400).Select(i => $"t{i}@x.com").ToArray();
        var bcc = Enumerable.Range(0, 101).Select(i => $"b{i}@x.com").ToArray();

        var ok = EmailRecipients.TryParse(Json(new { to, bcc }), out _, out var error);

        Assert.False(ok);
        Assert.Contains("500", error);
    }
}
