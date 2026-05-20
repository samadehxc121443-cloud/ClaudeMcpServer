namespace ClaudeMcpServer.Domain.ValueObjects;

/// <summary>
/// Standard JSON-RPC 2.0 error codes and a factory for common error objects.
/// See https://www.jsonrpc.org/specification#error_object for the full specification.
/// </summary>
public sealed class JsonRpcError
{
    /// <summary>Gets the numeric error code as defined by JSON-RPC 2.0.</summary>
    public int Code { get; }

    /// <summary>Gets the human-readable error message.</summary>
    public string Message { get; }

    /// <summary>Gets optional additional error data. May be null.</summary>
    public object? Data { get; }

    /// <summary>Initializes a new instance of <see cref="JsonRpcError"/>.</summary>
    public JsonRpcError(int code, string message, object? data = null)
    {
        Code = code;
        Message = message;
        Data = data;
    }

    /// <summary>JSON-RPC parse error code (-32700).</summary>
    public const int ParseError = -32700;
    /// <summary>JSON-RPC invalid request code (-32600).</summary>
    public const int InvalidRequest = -32600;
    /// <summary>JSON-RPC method not found code (-32601).</summary>
    public const int MethodNotFound = -32601;
    /// <summary>JSON-RPC invalid params code (-32602).</summary>
    public const int InvalidParams = -32602;
    /// <summary>JSON-RPC internal error code (-32603).</summary>
    public const int InternalError = -32603;

    /// <summary>Factory for a method-not-found error.</summary>
    public static JsonRpcError MethodNotFoundError(string method) =>
        new(MethodNotFound, $"Method not found: {method}");

    /// <summary>Factory for an internal error, wrapping an exception message.</summary>
    public static JsonRpcError FromException(Exception ex) =>
        new(InternalError, "Internal error", ex.Message);
}
