using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Response payload for the "initialize" method, describing this server's identity and capabilities.</summary>
public sealed class InitializeResult
{
    /// <summary>Gets the MCP protocol version this server implements.</summary>
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";

    /// <summary>Gets metadata about this server implementation.</summary>
    [JsonPropertyName("serverInfo")]
    public ServerInfo ServerInfo { get; init; } = new();

    /// <summary>Gets the capabilities advertised by this server.</summary>
    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; init; } = new();
}

/// <summary>Identifies this MCP server to connecting clients.</summary>
public sealed class ServerInfo
{
    /// <summary>Gets the server name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "ClaudeMcpServer";

    /// <summary>Gets the server version string.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";
}

/// <summary>Advertises the features this MCP server supports.</summary>
public sealed class ServerCapabilities
{
    /// <summary>Gets the tools capability object, indicating this server exposes callable tools.</summary>
    [JsonPropertyName("tools")]
    public ToolsCapability Tools { get; init; } = new();
}

/// <summary>Declares tool-related server capabilities.</summary>
public sealed class ToolsCapability
{
    /// <summary>Gets a value indicating whether the server can send tool list change notifications.</summary>
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; } = false;
}
