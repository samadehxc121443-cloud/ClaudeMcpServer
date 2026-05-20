using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

/// <summary>Tests for <see cref="SystemInfoTool"/>.</summary>
public class SystemInfoToolTests
{
    /// <summary>Verifies the tool name is correct.</summary>
    [Fact]
    public void ToolName_Is_get_system_info()
    {
        var tool = new SystemInfoTool();
        Assert.Equal("get_system_info", tool.ToolName);
    }

    /// <summary>Verifies the tool returns non-empty content.</summary>
    [Fact]
    public async Task ExecuteAsync_Returns_NonEmpty_Content()
    {
        var tool = new SystemInfoTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Content);
    }

    /// <summary>Verifies .NET version is included in the output.</summary>
    [Fact]
    public async Task ExecuteAsync_Content_Contains_DotNet_Version()
    {
        var tool = new SystemInfoTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);

        Assert.Contains(".NET", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
