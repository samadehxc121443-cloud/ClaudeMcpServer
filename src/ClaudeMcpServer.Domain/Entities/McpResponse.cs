using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Domain.Entities;

/// <summary>
/// Represents a JSON-RPC 2.0 response to be sent back to the MCP client.
/// Exactly one of <see cref="Result"/> or <see cref="Error"/> is non-null.
/// </summary>
public sealed class McpResponse
{
    /// <summary>Gets the JSON-RPC protocol version. Always "2.0".</summary>
    public string JsonRpc { get; } = "2.0";

    /// <summary>Gets the identifier matching the originating request.</summary>
    public object? Id { get; }

    /// <summary>Gets the successful result payload. Null when the response is an error.</summary>
    public object? Result { get; }

    /// <summary>Gets the error payload. Null when the response is successful.</summary>
    public JsonRpcError? Error { get; }

    private McpResponse(object? id, object? result, JsonRpcError? error)
    {
        Id = id;
        Result = result;
        Error = error;
    }

    /// <summary>Creates a success response with the given result payload.</summary>
    public static McpResponse Success(object? id, object result) => new(id, result, null);

    /// <summary>Creates an error response with the given error payload.</summary>
    public static McpResponse Failure(object? id, JsonRpcError error) => new(id, null, error);
}
