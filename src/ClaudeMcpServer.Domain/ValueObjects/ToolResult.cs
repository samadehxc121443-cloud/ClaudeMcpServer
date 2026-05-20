namespace ClaudeMcpServer.Domain.ValueObjects;

/// <summary>
/// Represents the outcome of a tool execution, carrying either a text result or an error description.
/// </summary>
public sealed class ToolResult
{
    /// <summary>Gets the text content returned by the tool on success.</summary>
    public string Content { get; }

    /// <summary>Gets a value indicating whether this result represents an error.</summary>
    public bool IsError { get; }

    private ToolResult(string content, bool isError)
    {
        Content = content;
        IsError = isError;
    }

    /// <summary>Creates a successful tool result with the given content.</summary>
    public static ToolResult Success(string content) => new(content, false);

    /// <summary>Creates an error tool result with the given error message.</summary>
    public static ToolResult Error(string message) => new(message, true);
}
