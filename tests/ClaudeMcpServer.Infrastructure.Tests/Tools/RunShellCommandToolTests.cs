using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

/// <summary>Tests for <see cref="RunShellCommandTool"/>.</summary>
public class RunShellCommandToolTests
{
    /// <summary>Verifies the tool name is correct.</summary>
    [Fact]
    public void ToolName_Is_run_shell_command()
    {
        var tool = new RunShellCommandTool();
        Assert.Equal("run_shell_command", tool.ToolName);
    }

    /// <summary>Verifies non-whitelisted commands are rejected.</summary>
    [Fact]
    public async Task ExecuteAsync_Rejects_Non_Whitelisted_Command()
    {
        var tool = new RunShellCommandTool();
        var paramsJson = JsonSerializer.Serialize(new { command = "rm", args = "-rf /" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("not allowed", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies an error is returned when command parameter is missing.</summary>
    [Fact]
    public async Task ExecuteAsync_Rejects_Missing_Command()
    {
        var tool = new RunShellCommandTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);
        Assert.True(result.IsError);
    }

    /// <summary>Verifies arguments with shell metacharacters are rejected.</summary>
    [Fact]
    public async Task ExecuteAsync_Rejects_Shell_Metacharacters_In_Args()
    {
        var tool = new RunShellCommandTool();
        var paramsJson = JsonSerializer.Serialize(new { command = "echo", args = "hello; rm -rf /" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("metacharacters", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
