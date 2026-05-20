using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Parameters for the "tools/call" method, identifying the tool and its input arguments.</summary>
public sealed class CallToolParams
{
    /// <summary>Gets the name of the tool to invoke.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the raw JSON arguments for the tool.</summary>
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
}
