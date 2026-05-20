using System.Text.Json.Nodes;

namespace ClaudeMcpServer.Domain.Entities;

/// <summary>
/// Describes a tool exposed by the MCP server: its name, description, and JSON Schema for parameters.
/// Serialized directly into the tools/list response consumed by Claude.
/// </summary>
public sealed class ToolDefinition
{
    /// <summary>Gets the unique snake_case tool name (e.g. "get_system_info").</summary>
    public string Name { get; }

    /// <summary>Gets the human-readable description of what the tool does, shown to Claude.</summary>
    public string Description { get; }

    /// <summary>Gets the JSON Schema object describing accepted parameters.</summary>
    public JsonObject InputSchema { get; }

    /// <summary>Initializes a new <see cref="ToolDefinition"/>.</summary>
    public ToolDefinition(string name, string description, JsonObject inputSchema)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
    }
}
