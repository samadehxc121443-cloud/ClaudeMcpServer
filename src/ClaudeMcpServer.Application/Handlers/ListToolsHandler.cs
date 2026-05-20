using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;

namespace ClaudeMcpServer.Application.Handlers;

/// <summary>
/// Handles the "tools/list" JSON-RPC method.
/// Returns all tools registered in the <see cref="IToolRegistry"/>.
/// </summary>
public sealed class ListToolsHandler : IMcpRequestHandler
{
    private readonly IToolRegistry _registry;

    /// <summary>Initializes a new instance of <see cref="ListToolsHandler"/>.</summary>
    public ListToolsHandler(IToolRegistry registry) => _registry = registry;

    /// <inheritdoc/>
    public string Method => "tools/list";

    /// <inheritdoc/>
    public Task<object?> HandleAsync(McpRequest request, CancellationToken ct)
    {
        var tools = _registry.GetAll()
            .Select(h =>
            {
                var def = h.GetDefinition();
                return new ToolInfo
                {
                    Name = def.Name,
                    Description = def.Description,
                    InputSchema = def.InputSchema
                };
            })
            .ToList();

        return Task.FromResult<object?>(new ListToolsResult { Tools = tools });
    }
}
