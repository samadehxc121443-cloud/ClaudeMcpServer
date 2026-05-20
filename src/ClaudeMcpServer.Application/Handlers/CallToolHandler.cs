using System.Text.Json;
using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Application.Handlers;

/// <summary>
/// Handles the "tools/call" JSON-RPC method.
/// Deserializes <see cref="CallToolParams"/>, looks up the tool by name in the registry,
/// and dispatches execution to the appropriate <see cref="IToolHandler"/>.
/// </summary>
public sealed class CallToolHandler : IMcpRequestHandler
{
    private readonly IToolRegistry _registry;
    private readonly ILogger<CallToolHandler> _logger;

    private static readonly JsonSerializerOptions DeserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Initializes a new instance of <see cref="CallToolHandler"/>.</summary>
    public CallToolHandler(IToolRegistry registry, ILogger<CallToolHandler> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Method => "tools/call";

    /// <inheritdoc/>
    public async Task<object?> HandleAsync(McpRequest request, CancellationToken ct)
    {
        if (request.Params is not { } paramsElement)
            return ErrorResult("Missing params in tools/call request");

        CallToolParams? callParams;
        try
        {
            callParams = paramsElement.Deserialize<CallToolParams>(DeserializeOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize tools/call params");
            return ErrorResult("Invalid tools/call parameters");
        }

        if (callParams is null || string.IsNullOrWhiteSpace(callParams.Name))
            return ErrorResult("Tool name is required");

        var tool = _registry.GetByName(callParams.Name);
        if (tool is null)
        {
            _logger.LogWarning("Tool not found: {ToolName}", callParams.Name);
            return ErrorResult($"Unknown tool: {callParams.Name}");
        }

        var arguments = callParams.Arguments ?? default;
        try
        {
            var toolResult = await tool.ExecuteAsync(arguments, ct);
            return new CallToolResult(toolResult.Content, toolResult.IsError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} threw an unhandled exception", callParams.Name);
            return ErrorResult($"Tool execution failed: {ex.Message}");
        }
    }

    private static object ErrorResult(string message) => new CallToolResult(message, true);
}

/// <summary>Serializable result for the "tools/call" response.</summary>
/// <param name="Content">The text output from the tool.</param>
/// <param name="IsError">True if the tool returned an error rather than a success value.</param>
public sealed record CallToolResult(string Content, bool IsError)
{
    /// <summary>Gets the content array in MCP format, containing a single text item.</summary>
    public IReadOnlyList<ContentItem> content { get; } = [new ContentItem("text", Content)];

    /// <summary>Gets whether this result is an error.</summary>
    public bool isError { get; } = IsError;
}

/// <summary>A single content item within a tool call result.</summary>
/// <param name="Type">The content type, e.g. "text".</param>
/// <param name="Text">The text payload.</param>
public sealed record ContentItem(string Type, string Text)
{
    /// <summary>Gets the content type.</summary>
    public string type { get; } = Type;

    /// <summary>Gets the text content.</summary>
    public string text { get; } = Text;
}
