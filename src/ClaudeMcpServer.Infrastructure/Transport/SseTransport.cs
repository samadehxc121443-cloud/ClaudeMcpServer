using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Infrastructure.Transport;

/// <summary>
/// Stub implementation of the SSE (Server-Sent Events) transport for future HTTP-based MCP clients.
/// Not used at runtime — the stdio transport is active by default.
/// </summary>
public sealed class SseTransport : ITransport
{
    private readonly ILogger<SseTransport> _logger;

    /// <summary>Initializes a new instance of <see cref="SseTransport"/>.</summary>
    public SseTransport(ILogger<SseTransport> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<McpRequest?> ReadRequestAsync(CancellationToken ct)
    {
        _logger.LogWarning("SseTransport is not implemented; use StdioTransport instead");
        return Task.FromResult<McpRequest?>(null);
    }

    /// <inheritdoc/>
    public Task WriteResponseAsync(McpResponse response, CancellationToken ct)
    {
        _logger.LogWarning("SseTransport.WriteResponseAsync called but not implemented");
        return Task.CompletedTask;
    }
}
