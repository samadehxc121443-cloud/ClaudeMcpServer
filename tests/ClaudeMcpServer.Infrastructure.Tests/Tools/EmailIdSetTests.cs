using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

/// <summary>Tests for <see cref="EmailIdSet"/> parsing and the per-call email cap.</summary>
public class EmailIdSetTests
{
    private static JsonElement Json(object value) =>
        JsonSerializer.Deserialize<JsonElement>(JsonSerializer.Serialize(value));

    /// <summary>A single integer id yields one email (backward compatible).</summary>
    [Fact]
    public void TryParse_Accepts_Single_Integer()
    {
        var ok = EmailIdSet.TryParse(Json(new { id = 5 }), out var set, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal([5u], set.Ids);
    }

    /// <summary>An array of integers yields several emails (batch).</summary>
    [Fact]
    public void TryParse_Accepts_Integer_Array()
    {
        var ok = EmailIdSet.TryParse(Json(new { id = new[] { 5, 6, 7 } }), out var set, out _);

        Assert.True(ok);
        Assert.Equal([5u, 6u, 7u], set.Ids);
    }

    /// <summary>A missing id is rejected.</summary>
    [Fact]
    public void TryParse_Rejects_Missing_Id()
    {
        var ok = EmailIdSet.TryParse(Json(new { save_dir = "C:\\tmp" }), out _, out var error);

        Assert.False(ok);
        Assert.Contains("id", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>An empty array is rejected.</summary>
    [Fact]
    public void TryParse_Rejects_Empty_Array()
    {
        var ok = EmailIdSet.TryParse(Json(new { id = Array.Empty<int>() }), out _, out var error);

        Assert.False(ok);
        Assert.NotNull(error);
    }

    /// <summary>A non-integer entry is rejected.</summary>
    [Fact]
    public void TryParse_Rejects_Non_Integer_Entry()
    {
        var ok = EmailIdSet.TryParse(Json(new { id = new object[] { 5, "x" } }), out _, out var error);

        Assert.False(ok);
        Assert.Contains("integer", error, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Exactly the cap is allowed.</summary>
    [Fact]
    public void TryParse_Allows_Exactly_The_Cap()
    {
        var ids = Enumerable.Range(1, EmailIdSet.MaxEmailsPerCall).ToArray();

        var ok = EmailIdSet.TryParse(Json(new { id = ids }), out var set, out var error);

        Assert.True(ok);
        Assert.Null(error);
        Assert.Equal(EmailIdSet.MaxEmailsPerCall, set.Ids.Count);
    }

    /// <summary>Exceeding the cap is rejected with a clear message.</summary>
    [Fact]
    public void TryParse_Rejects_Over_The_Cap()
    {
        var ids = Enumerable.Range(1, EmailIdSet.MaxEmailsPerCall + 1).ToArray();

        var ok = EmailIdSet.TryParse(Json(new { id = ids }), out _, out var error);

        Assert.False(ok);
        Assert.Contains(EmailIdSet.MaxEmailsPerCall.ToString(), error);
    }
}
