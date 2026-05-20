using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Application.Services;

/// <summary>
/// Core MCP request processing loop.
/// Reads requests from the transport, dispatches them to registered handlers,
/// and writes responses back — running continuously until cancellation.
/// </summary>
public sealed class McpService
{
    private readonly ITransport _transport;
    private readonly IEnumerable<IMcpRequestHandler> _handlers;
    private readonly ILogger<McpService> _logger;

    /// <summary>Initializes a new instance of <see cref="McpService"/>.</summary>
    public McpService(
        ITransport transport,
        IEnumerable<IMcpRequestHandler> handlers,
        ILogger<McpService> logger)
    {
        _transport = transport;
        _handlers = handlers;
        _logger = logger;
    }

    /// <summary>
    /// Starts the request/response loop. Returns when the transport closes or cancellation is requested.
    /// </summary>
    public async Task RunAsync(CancellationToken ct)
    {
        _logger.LogInformation("MCP service started, waiting for requests");

        var handlerMap = _handlers.ToDictionary(h => h.Method, StringComparer.OrdinalIgnoreCase);

        while (!ct.IsCancellationRequested)
        {
            McpRequest? request;
            try
            {
                request = await _transport.ReadRequestAsync(ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Fatal error reading from transport");
                break;
            }

            if (request is null)
            {
                _logger.LogInformation("Transport closed — shutting down");
                break;
            }

            _logger.LogDebug("Received method: {Method}", request.Method);

            McpResponse response;
            if (handlerMap.TryGetValue(request.Method, out var handler))
            {
                try
                {
                    var result = await handler.HandleAsync(request, ct);
                    var id = ExtractId(request);
                    response = McpResponse.Success(id, result ?? new object());
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Handler for {Method} threw an exception", request.Method);
                    response = McpResponse.Failure(ExtractId(request), JsonRpcError.FromException(ex));
                }
            }
            else
            {
                _logger.LogWarning("No handler for method: {Method}", request.Method);
                response = McpResponse.Failure(
                    ExtractId(request),
                    JsonRpcError.MethodNotFoundError(request.Method));
            }

            try
            {
                await _transport.WriteResponseAsync(response, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error writing response for method {Method}", request.Method);
            }
        }

        _logger.LogInformation("MCP service stopped");
    }

    private static object? ExtractId(McpRequest request)
    {
        if (request.Id is not { } id) return null;
        return id.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Number => id.GetInt64(),
            System.Text.Json.JsonValueKind.String => id.GetString(),
            _ => null
        };
    }
}
