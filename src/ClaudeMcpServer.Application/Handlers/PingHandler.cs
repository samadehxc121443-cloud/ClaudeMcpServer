using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;

namespace ClaudeMcpServer.Application.Handlers;

/// <summary>
/// Handles the "ping" JSON-RPC method used for keep-alive health checks.
/// Returns an empty object as specified by the MCP protocol.
/// </summary>
public sealed class PingHandler : IMcpRequestHandler
{
    /// <inheritdoc/>
    public string Method => "ping";

    /// <inheritdoc/>
    public Task<object?> HandleAsync(McpRequest request, CancellationToken ct) =>
        Task.FromResult<object?>(new { });
}
