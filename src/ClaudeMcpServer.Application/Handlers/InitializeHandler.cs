using System.Text.Json;
using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;

namespace ClaudeMcpServer.Application.Handlers;

/// <summary>
/// Handles the "initialize" JSON-RPC method.
/// Returns server identity, protocol version, and advertised capabilities.
/// </summary>
public sealed class InitializeHandler : IMcpRequestHandler
{
    private const string FallbackProtocolVersion = "2024-11-05";

    /// <inheritdoc/>
    public string Method => "initialize";

    /// <inheritdoc/>
    public Task<object?> HandleAsync(McpRequest request, CancellationToken ct)
    {
        var negotiatedVersion = FallbackProtocolVersion;

        if (request.Params.HasValue &&
            request.Params.Value.ValueKind == JsonValueKind.Object &&
            request.Params.Value.TryGetProperty("protocolVersion", out var versionProp))
        {
            negotiatedVersion = versionProp.GetString() ?? FallbackProtocolVersion;
        }

        var result = new InitializeResult
        {
            ProtocolVersion = negotiatedVersion,
            ServerInfo = new ServerInfo { Name = "ClaudeMcpServer", Version = "1.0.0" },
            Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } }
        };
        return Task.FromResult<object?>(result);
    }
}
