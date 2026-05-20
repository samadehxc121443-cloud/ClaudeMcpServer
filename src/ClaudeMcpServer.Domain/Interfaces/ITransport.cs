using ClaudeMcpServer.Domain.Entities;

namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Abstracts the transport layer used to receive MCP requests and send responses.
/// Implementations include stdio (for Claude Desktop) and SSE (for HTTP clients).
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Reads the next incoming MCP request from the transport stream.
    /// Returns <c>null</c> when the stream is closed or EOF is reached.
    /// </summary>
    Task<McpRequest?> ReadRequestAsync(CancellationToken ct);

    /// <summary>Writes a serialized MCP response to the transport output stream.</summary>
    Task WriteResponseAsync(McpResponse response, CancellationToken ct);
}
