namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Provides lookup and enumeration of all registered <see cref="IToolHandler"/> instances.
/// Populated at startup via dependency injection — no manual registration required.
/// </summary>
public interface IToolRegistry
{
    /// <summary>Returns all registered tool handlers.</summary>
    IEnumerable<IToolHandler> GetAll();

    /// <summary>
    /// Looks up a tool handler by its <see cref="IToolHandler.ToolName"/>.
    /// Returns <c>null</c> when the tool name is not recognized.
    /// </summary>
    IToolHandler? GetByName(string toolName);
}
