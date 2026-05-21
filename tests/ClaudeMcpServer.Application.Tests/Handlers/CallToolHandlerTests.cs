using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClaudeMcpServer.Application.Tests.Handlers;

/// <summary>Tests for <see cref="CallToolHandler"/>.</summary>
public class CallToolHandlerTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string ToolName => "echo";
        public ToolDefinition GetDefinition() => new("echo", "Echoes input",
            new JsonObject { ["type"] = "object" });
        public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Success("echoed"));
    }

    private sealed class FakeRegistry : IToolRegistry
    {
        private readonly IToolHandler[] _handlers;
        public FakeRegistry(params IToolHandler[] handlers) => _handlers = handlers;
        public IEnumerable<IToolHandler> GetAll() => _handlers;
        public IToolHandler? GetByName(string name) => _handlers.FirstOrDefault(h => h.ToolName == name);
    }

    private sealed class ValidLicense : ILicenseService
    {
        public Task<LicenseResult> ValidateAsync(CancellationToken ct) =>
            Task.FromResult(LicenseResult.Valid("test"));
    }

    /// <summary>Verifies the handler dispatches to the correct tool and returns a result.</summary>
    [Fact]
    public async Task HandleAsync_Dispatches_To_Correct_Tool()
    {
        var registry = new FakeRegistry(new EchoTool());
        var handler = new CallToolHandler(registry, new ValidLicense(), NullLogger<CallToolHandler>.Instance);

        var paramsJson = JsonSerializer.Serialize(new { name = "echo", arguments = new { } });
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = JsonSerializer.Deserialize<JsonElement>(paramsJson)
        };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var callResult = Assert.IsType<CallToolResult>(result);
        Assert.False(callResult.IsError);
        Assert.Contains("echoed", callResult.Content[0].Text);
    }

    /// <summary>Verifies an error result is returned when the tool name is not found.</summary>
    [Fact]
    public async Task HandleAsync_Returns_Error_For_Unknown_Tool()
    {
        var registry = new FakeRegistry();
        var handler = new CallToolHandler(registry, new ValidLicense(), NullLogger<CallToolHandler>.Instance);

        var paramsJson = JsonSerializer.Serialize(new { name = "nonexistent", arguments = new { } });
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = JsonSerializer.Deserialize<JsonElement>(paramsJson)
        };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var callResult = Assert.IsType<CallToolResult>(result);
        Assert.True(callResult.IsError);
    }
}
