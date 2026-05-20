using System.Text.Json;
using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Domain.Entities;
using Xunit;

namespace ClaudeMcpServer.Application.Tests.Handlers;

/// <summary>Tests for <see cref="InitializeHandler"/>.</summary>
public class InitializeHandlerTests
{
    /// <summary>Verifies the handler returns a properly populated <see cref="InitializeResult"/>.</summary>
    [Fact]
    public async Task HandleAsync_Returns_InitializeResult_With_ServerInfo()
    {
        var handler = new InitializeHandler();
        var request = new McpRequest { Method = "initialize", Id = JsonSerializer.SerializeToElement(1) };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var initResult = Assert.IsType<InitializeResult>(result);
        Assert.Equal("2024-11-05", initResult.ProtocolVersion);
        Assert.Equal("ClaudeMcpServer", initResult.ServerInfo.Name);
    }

    /// <summary>Verifies the method name is "initialize".</summary>
    [Fact]
    public void Method_Is_Initialize()
    {
        var handler = new InitializeHandler();
        Assert.Equal("initialize", handler.Method);
    }
}
