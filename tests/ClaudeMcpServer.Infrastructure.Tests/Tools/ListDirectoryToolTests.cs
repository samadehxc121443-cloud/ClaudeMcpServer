using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

/// <summary>Tests for <see cref="ListDirectoryTool"/>.</summary>
public class ListDirectoryToolTests
{
    /// <summary>Verifies the tool name is correct.</summary>
    [Fact]
    public void ToolName_Is_list_directory()
    {
        var tool = new ListDirectoryTool();
        Assert.Equal("list_directory", tool.ToolName);
    }

    /// <summary>Verifies an existing directory can be listed.</summary>
    [Fact]
    public async Task ExecuteAsync_Lists_Controlled_Directory()
    {
        var tmpDir = Directory.CreateTempSubdirectory("mcp_test_");
        File.WriteAllText(Path.Combine(tmpDir.FullName, "test.txt"), "hello");

        try
        {
            var tool = new ListDirectoryTool();
            var paramsJson = JsonSerializer.Serialize(new { path = tmpDir.FullName });
            var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

            var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Contains("test.txt", result.Content);
        }
        finally
        {
            tmpDir.Delete(recursive: true);
        }
    }

    /// <summary>Verifies an error is returned for a nonexistent directory.</summary>
    [Fact]
    public async Task ExecuteAsync_Returns_Error_For_Nonexistent_Directory()
    {
        var tool = new ListDirectoryTool();
        var paramsJson = JsonSerializer.Serialize(new { path = "/nonexistent/path/xyz" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
    }

    /// <summary>Verifies an error is returned when path parameter is missing.</summary>
    [Fact]
    public async Task ExecuteAsync_Returns_Error_When_Path_Missing()
    {
        var tool = new ListDirectoryTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);
        Assert.True(result.IsError);
    }
}
