using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

/// <summary>Tests for <see cref="ReadFileTool"/>.</summary>
public class ReadFileToolTests
{
    /// <summary>Verifies the tool name is correct.</summary>
    [Fact]
    public void ToolName_Is_read_file()
    {
        var tool = new ReadFileTool();
        Assert.Equal("read_file", tool.ToolName);
    }

    /// <summary>Verifies an error is returned when path parameter is missing.</summary>
    [Fact]
    public async Task ExecuteAsync_Returns_Error_When_Path_Missing()
    {
        var tool = new ReadFileTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);
        Assert.True(result.IsError);
    }

    /// <summary>Verifies a text file can be read successfully.</summary>
    [Fact]
    public async Task ExecuteAsync_Returns_File_Content()
    {
        var tool = new ReadFileTool();
        var tmpFile = Path.ChangeExtension(Path.GetTempFileName(), ".txt");
        await File.WriteAllTextAsync(tmpFile, "hello test");

        try
        {
            var paramsJson = JsonSerializer.Serialize(new { path = tmpFile });
            var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

            var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Contains("hello test", result.Content);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    /// <summary>Verifies an error is returned for a nonexistent file.</summary>
    [Fact]
    public async Task ExecuteAsync_Returns_Error_For_Nonexistent_File()
    {
        var tool = new ReadFileTool();
        var paramsJson = JsonSerializer.Serialize(new { path = "/nonexistent/path/file.txt" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
    }

    /// <summary>Verifies files with disallowed extensions are rejected.</summary>
    [Fact]
    public async Task ExecuteAsync_Rejects_Disallowed_Extension()
    {
        var tool = new ReadFileTool();
        var tmpFile = Path.ChangeExtension(Path.GetTempFileName(), ".exe");
        await File.WriteAllBytesAsync(tmpFile, [0x4D, 0x5A, 0x00, 0x00]);

        try
        {
            var paramsJson = JsonSerializer.Serialize(new { path = tmpFile });
            var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

            var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

            Assert.True(result.IsError);
            Assert.Contains("not allowed", result.Content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
