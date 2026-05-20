using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Serializable representation of a tool for the "tools/list" response.</summary>
public sealed class ToolInfo
{
    /// <summary>Gets the snake_case tool name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the human-readable tool description shown to Claude.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the JSON Schema describing the tool's accepted parameters.</summary>
    [JsonPropertyName("inputSchema")]
    public JsonObject InputSchema { get; init; } = [];
}
