using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using Xunit;

namespace ClaudeMcpServer.Application.Tests.Handlers;

/// <summary>Tests for <see cref="ListToolsHandler"/>.</summary>
public class ListToolsHandlerTests
{
    private sealed class FakeTool : IToolHandler
    {
        public string ToolName => "test_tool";
        public ToolDefinition GetDefinition() => new("test_tool", "A test tool", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject()
        });
        public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Success("ok"));
    }

    private sealed class FakeRegistry : IToolRegistry
    {
        private readonly IToolHandler[] _handlers;
        public FakeRegistry(params IToolHandler[] handlers) => _handlers = handlers;
        public IEnumerable<IToolHandler> GetAll() => _handlers;
        public IToolHandler? GetByName(string name) => _handlers.FirstOrDefault(h => h.ToolName == name);
    }

    /// <summary>Verifies all registered tools are returned.</summary>
    [Fact]
    public async Task HandleAsync_Returns_All_Registered_Tools()
    {
        var registry = new FakeRegistry(new FakeTool());
        var handler = new ListToolsHandler(registry);
        var request = new McpRequest { Method = "tools/list" };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var listResult = Assert.IsType<ListToolsResult>(result);
        Assert.Single(listResult.Tools);
        Assert.Equal("test_tool", listResult.Tools[0].Name);
    }

    /// <summary>Verifies an empty list is returned when no tools are registered.</summary>
    [Fact]
    public async Task HandleAsync_Returns_Empty_List_When_No_Tools()
    {
        var registry = new FakeRegistry();
        var handler = new ListToolsHandler(registry);
        var request = new McpRequest { Method = "tools/list" };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var listResult = Assert.IsType<ListToolsResult>(result);
        Assert.Empty(listResult.Tools);
    }
}
