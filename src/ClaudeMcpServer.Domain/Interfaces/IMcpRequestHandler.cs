using ClaudeMcpServer.Domain.Entities;

namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Handles a specific JSON-RPC method (e.g. "initialize", "tools/list", "tools/call").
/// One implementation per MCP method.
/// </summary>
public interface IMcpRequestHandler
{
    /// <summary>Gets the JSON-RPC method name this handler responds to (e.g. "tools/list").</summary>
    string Method { get; }

    /// <summary>
    /// Processes the request and produces a response payload to be serialized into a JSON-RPC result.
    /// </summary>
    Task<object?> HandleAsync(McpRequest request, CancellationToken ct);
}
