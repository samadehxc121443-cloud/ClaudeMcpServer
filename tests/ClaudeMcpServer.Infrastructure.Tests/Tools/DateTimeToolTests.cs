using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

/// <summary>Tests for <see cref="DateTimeTool"/>.</summary>
public class DateTimeToolTests
{
    /// <summary>Verifies the tool name is correct.</summary>
    [Fact]
    public void ToolName_Is_get_datetime()
    {
        var tool = new DateTimeTool();
        Assert.Equal("get_datetime", tool.ToolName);
    }

    /// <summary>Verifies the output contains the current year.</summary>
    [Fact]
    public async Task ExecuteAsync_Returns_Current_Year()
    {
        var tool = new DateTimeTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains(DateTime.UtcNow.Year.ToString(), result.Content);
    }

    /// <summary>Verifies timezone parameter is handled correctly.</summary>
    [Fact]
    public async Task ExecuteAsync_With_UTC_Timezone_Returns_UTC_Label()
    {
        var tool = new DateTimeTool();
        var paramsJson = JsonSerializer.Serialize(new { timezone = "UTC" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("UTC", result.Content);
    }

    /// <summary>Verifies an invalid timezone returns an error.</summary>
    [Fact]
    public async Task ExecuteAsync_With_Invalid_Timezone_Returns_Error()
    {
        var tool = new DateTimeTool();
        var paramsJson = JsonSerializer.Serialize(new { timezone = "Not/AReal/Zone" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
    }
}
