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

    private sealed class SpyTool(Action onExecute) : IToolHandler
    {
        public string ToolName => "spy";
        public ToolDefinition GetDefinition() => new("spy", "Spy tool", new JsonObject { ["type"] = "object" });
        public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
        {
            onExecute();
            return Task.FromResult(ToolResult.Success("ok"));
        }
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

        public Task<UsageStatus> CheckUsageAsync(string operation, int count, CancellationToken ct) =>
            Task.FromResult(UsageStatus.Untracked());

        public Task<UsageStatus> RecordUsageAsync(string operation, int count, CancellationToken ct) =>
            Task.FromResult(UsageStatus.Untracked());
    }

    private sealed class InvalidLicense(string reason = "License revoked.") : ILicenseService
    {
        public Task<LicenseResult> ValidateAsync(CancellationToken ct) =>
            Task.FromResult(LicenseResult.Invalid(reason));

        public Task<UsageStatus> CheckUsageAsync(string operation, int count, CancellationToken ct) =>
            Task.FromResult(UsageStatus.Untracked());

        public Task<UsageStatus> RecordUsageAsync(string operation, int count, CancellationToken ct) =>
            Task.FromResult(UsageStatus.Untracked());
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

    /// <summary>Verifies a service-unavailable error is returned when the license is invalid.</summary>
    [Fact]
    public async Task HandleAsync_Returns_Error_When_License_Is_Invalid()
    {
        var registry = new FakeRegistry(new EchoTool());
        var handler = new CallToolHandler(registry, new InvalidLicense("Key revoked."), NullLogger<CallToolHandler>.Instance);

        var paramsJson = JsonSerializer.Serialize(new { name = "echo", arguments = new { } });
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = JsonSerializer.Deserialize<JsonElement>(paramsJson)
        };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var callResult = Assert.IsType<CallToolResult>(result);
        Assert.True(callResult.IsError);
        Assert.Contains("Key revoked.", callResult.Content[0].Text);
    }

    /// <summary>Verifies the tool is never executed when the license check fails.</summary>
    [Fact]
    public async Task HandleAsync_Does_Not_Execute_Tool_When_License_Invalid()
    {
        var executed = false;
        var spy = new SpyTool(() => executed = true);
        var registry = new FakeRegistry(spy);
        var handler = new CallToolHandler(registry, new InvalidLicense(), NullLogger<CallToolHandler>.Instance);

        var paramsJson = JsonSerializer.Serialize(new { name = "spy", arguments = new { } });
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = JsonSerializer.Deserialize<JsonElement>(paramsJson)
        };

        await handler.HandleAsync(request, CancellationToken.None);

        Assert.False(executed);
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
