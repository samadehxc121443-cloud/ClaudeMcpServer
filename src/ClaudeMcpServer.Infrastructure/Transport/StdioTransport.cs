using System.Text;
using System.Text.Json;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Infrastructure.Transport;

/// <summary>
/// Implements the MCP stdio transport: reads newline-delimited JSON-RPC requests from stdin
/// and writes JSON-RPC responses to stdout. All logging goes to stderr to avoid corrupting the protocol stream.
/// </summary>
public sealed class StdioTransport : ITransport
{
    private readonly ILogger<StdioTransport> _logger;

    private static readonly JsonSerializerOptions WriteOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    /// <summary>Initializes a new instance of <see cref="StdioTransport"/>.</summary>
    public StdioTransport(ILogger<StdioTransport> logger)
    {
        _logger = logger;
        // Ensure stdin/stdout use UTF-8 without BOM
        Console.InputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        Console.OutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
    }

    /// <inheritdoc/>
    public async Task<McpRequest?> ReadRequestAsync(CancellationToken ct)
    {
        var line = await Console.In.ReadLineAsync(ct);
        if (line is null) return null;

        line = line.Trim();
        if (string.IsNullOrEmpty(line)) return null;

        try
        {
            var doc = JsonDocument.Parse(line);
            var root = doc.RootElement;

            return new McpRequest
            {
                JsonRpc = root.TryGetProperty("jsonrpc", out var jsonrpc) ? jsonrpc.GetString() ?? "2.0" : "2.0",
                Id = root.TryGetProperty("id", out var id) ? id : null,
                Method = root.TryGetProperty("method", out var method) ? method.GetString() ?? string.Empty : string.Empty,
                Params = root.TryGetProperty("params", out var p) ? p : null
            };
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse incoming JSON line");
            return null;
        }
    }

    /// <inheritdoc/>
    public async Task WriteResponseAsync(McpResponse response, CancellationToken ct)
    {
        var payload = BuildPayload(response);
        var json = JsonSerializer.Serialize(payload, WriteOptions);
        await Console.Out.WriteLineAsync(json.AsMemory(), ct);
        await Console.Out.FlushAsync(ct);
    }

    private static object BuildPayload(McpResponse response)
    {
        if (response.Error is { } error)
        {
            return new
            {
                jsonrpc = response.JsonRpc,
                id = response.Id,
                error = new { code = error.Code, message = error.Message, data = error.Data }
            };
        }

        return new
        {
            jsonrpc = response.JsonRpc,
            id = response.Id,
            result = response.Result
        };
    }
}
