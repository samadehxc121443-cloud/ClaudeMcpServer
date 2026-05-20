using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Response payload for the "tools/list" method.</summary>
public sealed class ListToolsResult
{
    /// <summary>Gets all tools currently registered in this server.</summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<ToolInfo> Tools { get; init; } = [];
}
