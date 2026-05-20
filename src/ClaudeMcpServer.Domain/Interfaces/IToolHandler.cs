using System.Text.Json;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Defines a single MCP tool that can be discovered and invoked by Claude Desktop.
/// Implement this interface to add a new tool — no other changes are required.
/// </summary>
public interface IToolHandler
{
    /// <summary>Gets the unique snake_case tool name exposed to Claude (e.g. "get_system_info").</summary>
    string ToolName { get; }

    /// <summary>Returns the full tool definition including description and JSON schema for parameters.</summary>
    ToolDefinition GetDefinition();

    /// <summary>
    /// Executes the tool with the given parameters.
    /// </summary>
    /// <param name="parameters">The JSON element containing the tool's input parameters.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="ToolResult"/> containing the tool output or error details.</returns>
    Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct);
}
