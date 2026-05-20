using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Application.Services;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Infrastructure.Configuration;
using ClaudeMcpServer.Infrastructure.Registry;
using ClaudeMcpServer.Infrastructure.Tools;
using ClaudeMcpServer.Infrastructure.Transport;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

// Redirect all .NET logging to stderr so stdout remains clean for JSON-RPC
var host = Host.CreateDefaultBuilder(args)
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole(opts =>
        {
            // Write all log levels to stderr, keeping stdout clean for JSON-RPC output
            opts.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    })
    .ConfigureServices((context, services) =>
    {
        // Transport — stdio is the MCP-standard transport for Claude Desktop
        services.AddSingleton<ITransport, StdioTransport>();

        // Tool registry — discovers all IToolHandler registrations automatically
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // Email settings — bound from appsettings.json "Email" section
        services.Configure<EmailSettings>(context.Configuration.GetSection("Email"));

        // Built-in tools — add new tools here with AddSingleton<IToolHandler, YourTool>()
        services.AddSingleton<IToolHandler, SystemInfoTool>();
        services.AddSingleton<IToolHandler, DateTimeTool>();
        services.AddSingleton<IToolHandler, ReadFileTool>();
        services.AddSingleton<IToolHandler, RunShellCommandTool>();
        services.AddSingleton<IToolHandler, ListDirectoryTool>();

        // Email tools — iCloud IMAP/SMTP via MailKit
        services.AddSingleton<IToolHandler, ListEmailsTool>();
        services.AddSingleton<IToolHandler, ReadEmailTool>();
        services.AddSingleton<IToolHandler, SearchEmailsTool>();
        services.AddSingleton<IToolHandler, SendEmailTool>();

        // MCP method handlers
        services.AddSingleton<IMcpRequestHandler, InitializeHandler>();
        services.AddSingleton<IMcpRequestHandler, ListToolsHandler>();
        services.AddSingleton<IMcpRequestHandler, CallToolHandler>();
        services.AddSingleton<IMcpRequestHandler, PingHandler>();

        // Core MCP service
        services.AddSingleton<McpService>();

        // Hosted service that drives the MCP loop
        services.AddHostedService<McpHostedService>();
    })
    .Build();

await host.RunAsync();

/// <summary>
/// BackgroundService that starts the MCP request loop as a hosted service,
/// enabling clean startup/shutdown integration with the .NET Generic Host.
/// </summary>
internal sealed class McpHostedService : BackgroundService
{
    private readonly McpService _mcpService;
    private readonly ILogger<McpHostedService> _logger;

    /// <summary>Initializes a new instance of <see cref="McpHostedService"/>.</summary>
    public McpHostedService(McpService mcpService, ILogger<McpHostedService> logger)
    {
        _mcpService = mcpService;
        _logger = logger;
    }

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ClaudeMcpServer starting — listening on stdio");
        try
        {
            await _mcpService.RunAsync(stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogCritical(ex, "McpService terminated unexpectedly");
            throw;
        }
        _logger.LogInformation("ClaudeMcpServer stopped");
    }
}
