using ClaudeMcpServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Infrastructure.Registry;

/// <summary>
/// Resolves all registered <see cref="IToolHandler"/> instances injected via DI.
/// Auto-discovers tools: any class implementing <see cref="IToolHandler"/> registered in the DI container
/// is automatically available without any changes to this class.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private readonly IReadOnlyDictionary<string, IToolHandler> _tools;
    private readonly ILogger<ToolRegistry> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="ToolRegistry"/>, consuming all DI-registered tool handlers.
    /// </summary>
    public ToolRegistry(IEnumerable<IToolHandler> handlers, ILogger<ToolRegistry> logger)
    {
        _logger = logger;
        _tools = handlers.ToDictionary(h => h.ToolName, StringComparer.OrdinalIgnoreCase);
        _logger.LogInformation("ToolRegistry initialized with {Count} tool(s): {Names}",
            _tools.Count, string.Join(", ", _tools.Keys));
    }

    /// <inheritdoc/>
    public IEnumerable<IToolHandler> GetAll() => _tools.Values;

    /// <inheritdoc/>
    public IToolHandler? GetByName(string toolName)
    {
        _tools.TryGetValue(toolName, out var handler);
        return handler;
    }
}
