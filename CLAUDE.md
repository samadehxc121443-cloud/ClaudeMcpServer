# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## .NET SDK

The .NET 10 SDK is installed at a non-standard path. Always use it explicitly:

```powershell
& "C:\Users\Jorge López\.dotnet10\dotnet" <command>
```

## Essential Commands

```powershell
# Build (must be 0 errors, 0 warnings)
& "C:\Users\Jorge López\.dotnet10\dotnet" build ClaudeMcpServer.sln

# Run all tests
& "C:\Users\Jorge López\.dotnet10\dotnet" test ClaudeMcpServer.sln

# Run a single test class
& "C:\Users\Jorge López\.dotnet10\dotnet" test tests/ClaudeMcpServer.Infrastructure.Tests/ --filter "ClassName=ListDirectoryToolTests"

# Smoke-test the server (PowerShell)
echo '{"jsonrpc":"2.0","id":1,"method":"ping","params":{}}' | & "C:\Users\Jorge López\.dotnet10\dotnet" run --project src/ClaudeMcpServer.Host/

# Publish self-contained binary for Claude Desktop on Windows (path must have no spaces or accented chars)
& "C:\Users\Jorge López\.dotnet10\dotnet" publish "C:\Users\Jorge López\OneDrive\Desktop\MCP mac\src\ClaudeMcpServer.Host\ClaudeMcpServer.Host.csproj" -c Release -r win-x64 --self-contained -o "C:\ClaudeMCP"

# Publish for macOS Apple Silicon
& "C:\Users\Jorge López\.dotnet10\dotnet" publish src/ClaudeMcpServer.Host/ -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64
```

## Architecture

Clean Architecture with strict dependency direction: `Domain ← Application ← Infrastructure ← Host`.

- **Domain** — interfaces (`IToolHandler`, `IToolRegistry`, `ITransport`, `IMcpRequestHandler`), entities (`McpRequest`, `McpResponse`, `ToolDefinition`), value objects (`ToolResult`, `JsonRpcError`). Zero external dependencies.
- **Application** — `McpService` (the JSON-RPC request loop), four method handlers (`InitializeHandler`, `ListToolsHandler`, `CallToolHandler`, `PingHandler`), and DTOs. Depends only on Domain.
- **Infrastructure** — `StdioTransport`, `ToolRegistry`, all tool implementations, `EmailSettings` config. Depends on Domain + Application.
- **Host** — `Program.cs` wires DI and runs `McpHostedService : BackgroundService`. The only place where concrete types are registered.

## The Extension Point

Adding a new tool requires exactly two changes:

1. Create `src/ClaudeMcpServer.Infrastructure/Tools/MyTool.cs` implementing `IToolHandler` (`ToolName`, `GetDefinition()`, `ExecuteAsync(JsonElement, CancellationToken)`).
2. Register in `Program.cs`: `services.AddSingleton<IToolHandler, MyTool>();`

`ToolRegistry` auto-discovers all `IToolHandler` registrations via `IEnumerable<IToolHandler>` DI injection — no other changes needed.

## LicenseServer Architecture

`ClaudeMcpServer.LicenseServer` is a standalone ASP.NET Core Minimal API deployed on Railway. Its internal structure uses:

- **Repository Pattern** — `ILicenseKeyRepository`, `ISessionTokenRepository` abstract all DB access.
- **Unit of Work** — `IUnitOfWork` / `UnitOfWork` wraps `DbContext.SaveChangesAsync` so services never call it directly.
- **Service Layer** — `ILicenseManagerService` / `LicenseManagerService` holds all business logic.
- **Decorator Pattern** — `LoggingLicenseManagerService` wraps `ILicenseManagerService` and adds structured logging without modifying the original.
- **Generic Interface** — `IRepository<T>` (in progress) defines the common `AddAsync` contract shared by both repositories.

DI registrations in `LicenseServer/Program.cs`:
```csharp
builder.Services.AddScoped<ILicenseKeyRepository, LicenseKeyRepository>();
builder.Services.AddScoped<ISessionTokenRepository, SessionTokenRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<LicenseManagerService>();
builder.Services.AddScoped<ILicenseManagerService, LoggingLicenseManagerService>();
```

**Security:** `AdminKey` lives only as a Railway environment variable — never in code or `appsettings.json`. Admin endpoints are protected by `AdminKeyFilter`.

## Critical Protocol Constraint

**stdout must contain only valid JSON-RPC 2.0.** All logging goes to stderr. `StdioTransport` enforces this by setting `LogToStandardErrorThreshold = LogLevel.Trace` and writing UTF-8 no-BOM on both stdin/stdout. Never use `Console.WriteLine` anywhere except inside `StdioTransport`.

## Email Tools

The four email tools (`list_emails`, `read_email`, `search_emails`, `send_email`) connect to iCloud via MailKit. Credentials are in `src/ClaudeMcpServer.Host/appsettings.json` under the `Email` section. `Password` must be an **app-specific password** from appleid.apple.com — never the Apple ID password.

The `appsettings.json` in the repo contains a placeholder. Set the real password locally only; do not commit it.

After setting the real password, run this once to prevent accidental commits:
```powershell
git update-index --assume-unchanged src/ClaudeMcpServer.Host/appsettings.json
```

## Claude Desktop Config (Windows)

`%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "claude-mcp-server": {
      "command": "C:\\ClaudeMCP\\ClaudeMcpServer.Host.exe",
      "args": [],
      "env": {}
    }
  }
}
```

After any code change, re-run the publish command and restart Claude Desktop.
