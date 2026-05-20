using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Parameters sent by the MCP client in the "initialize" request.</summary>
public sealed class InitializeParams
{
    /// <summary>Gets the protocol version string requested by the client (e.g. "2024-11-05").</summary>
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = string.Empty;

    /// <summary>Gets information about the connecting client application.</summary>
    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; init; }

    /// <summary>Gets client-declared capabilities. Currently informational only.</summary>
    [JsonPropertyName("capabilities")]
    public object? Capabilities { get; init; }
}

/// <summary>Identifies the client application connecting to this MCP server.</summary>
public sealed class ClientInfo
{
    /// <summary>Gets the client application name (e.g. "Claude Desktop").</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the client application version string.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}
