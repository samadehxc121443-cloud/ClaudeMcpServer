using System.Text.Json;

namespace ClaudeMcpServer.Domain.Entities;

/// <summary>
/// Represents a parsed JSON-RPC 2.0 request received from a MCP client such as Claude Desktop.
/// </summary>
public sealed class McpRequest
{
    /// <summary>Gets the JSON-RPC protocol version. Always "2.0".</summary>
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>Gets the request identifier. May be a string, number, or null (for notifications).</summary>
    public JsonElement? Id { get; init; }

    /// <summary>Gets the method name to invoke (e.g. "initialize", "tools/list", "tools/call").</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Gets the raw parameter payload. Callers parse this into method-specific DTOs.</summary>
    public JsonElement? Params { get; init; }
}
