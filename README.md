# ClaudeMcpServer

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)](#quick-start)

A production-ready **Model Context Protocol (MCP) server** written in C# .NET 10.  
Connects [Claude Desktop](https://claude.ai/download) on Windows and macOS to a set of local tools via the MCP stdio transport, with a cloud-hosted license server for per-client access control.

---

## Architecture

```
┌────────────────────────────────────────────────────────────────────┐
│                        ClaudeMcpServer.Host                         │
│  (stdio binary — runs locally on the client machine)                │
│                                                                      │
│  ┌──────────┐  ┌─────────────────┐  ┌──────────────────────────┐   │
│  │  Domain  │◄─│  Application    │◄─│      Infrastructure      │   │
│  │          │  │                 │  │                          │   │
│  │Interfaces│  │  McpService     │  │  StdioTransport          │   │
│  │Entities  │  │  Handlers:      │  │  ToolRegistry            │   │
│  │ValueObjs │  │  - Initialize   │  │  Tools (see below)       │   │
│  │          │  │  - ListTools    │  │  LicenseService          │   │
│  └──────────┘  │  - CallTool ────┼──┤   (token cache, 1h TTL) │   │
│                │  - Ping         │  └──────────────────────────┘   │
│                └─────────────────┘                                   │
└───────────────────────────┬────────────────────────────────────────┘
                            │ stdio (JSON-RPC 2.0)
                            ▼
                      Claude Desktop

                            │ HTTPS (token exchange, once per hour)
                            ▼
┌────────────────────────────────────────────────────────────────────┐
│                    ClaudeMcpServer.LicenseServer                    │
│  (ASP.NET Core Minimal API — deployed on Railway)                   │
│                                                                      │
│  POST /api/auth/token      — exchange API key for session token      │
│  POST /api/license/validate — validate API key directly             │
│  GET  /health              — DB connectivity check                   │
│  POST /api/admin/keys      — create license key (admin)             │
│  GET  /api/admin/keys      — list all keys (admin)                  │
│  DELETE /api/admin/keys/:id — revoke key (admin)                    │
└────────────────────────────────────────────────────────────────────┘
```

**Dependency rule:** `Domain ← Application ← Infrastructure ← Host`.  
**Extension point:** Adding a new tool requires exactly one class and one DI registration — no changes to the protocol layer.

---

## License System

Each client installation has an API key stored in `appsettings.json`. On startup and every hour, `LicenseService` exchanges the API key for a short-lived **session token** issued by the license server. Tool calls validate the token from memory (no network round-trip per call).

| Feature | Description |
|---|---|
| Token rotating auth | API key exchanges for a 1-hour session token, cached in memory |
| Per-request enforcement | `CallToolHandler` validates the cached token on every `tools/call` |
| Subscription expiry | Keys can have a `PlanName` and `ExpiresAt` date — expired keys are rejected |
| Admin-protected endpoints | `POST/GET/DELETE /api/admin/keys` require `X-Admin-Key` header |
| Health check | `GET /health` returns DB status and key count — used by Railway |

---

## Built-in Tools

### System Tools

| Tool | Description |
|---|---|
| `get_system_info` | OS, CPU architecture, .NET version, hostname, working set |
| `get_datetime` | Current date/time in multiple formats with optional IANA timezone |
| `read_file` | Read text/code files up to 1 MB (whitelisted extensions) |
| `list_directory` | List directory contents with type, size, and modification date |

### Email Tools (iCloud / me.com)

| Tool | Description |
|---|---|
| `list_emails` | List recent emails from the iCloud inbox (sender, subject, date, preview) |
| `read_email` | Read the full content of an email by its unique ID |
| `search_emails` | Search inbox by subject, sender, body text, or all fields |
| `send_email` | Send an email with optional HTML body and file attachments |
| `get_email_attachments` | Extract attachments from a specific email |

> **Setup:** Email tools require an [app-specific password](https://support.apple.com/en-us/102654) generated at appleid.apple.com. Set it in `appsettings.json` under `Email.Password`. Never commit real credentials.

---

## Quick Start

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Claude Desktop](https://claude.ai/download)

### 1. Clone and build

```bash
git clone https://github.com/samadehxc121443-cloud/ClaudeMcpServer.git
cd ClaudeMcpServer
dotnet build ClaudeMcpServer.sln
```

### 2. Run tests

```bash
dotnet test ClaudeMcpServer.sln
```

### 3. Publish a self-contained binary

```bash
# Windows x64
dotnet publish src/ClaudeMcpServer.Host/ -c Release -r win-x64 --self-contained -o ./publish/win-x64

# macOS Apple Silicon (M1/M2/M3)
dotnet publish src/ClaudeMcpServer.Host/ -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64
```

### 4. Configure Claude Desktop

**Windows** — edit `%APPDATA%\Claude\claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "claude-mcp-server": {
      "command": "C:\\publish\\win-x64\\ClaudeMcpServer.Host.exe",
      "args": [],
      "env": {}
    }
  }
}
```

**macOS** — edit `~/Library/Application Support/Claude/claude_desktop_config.json`:

```json
{
  "mcpServers": {
    "claude-mcp-server": {
      "command": "/absolute/path/to/publish/osx-arm64/ClaudeMcpServer.Host",
      "args": [],
      "env": {}
    }
  }
}
```

Restart Claude Desktop. The tools will appear in Claude's tool panel.

---

## Adding a New Tool

**Step 1** — Create `src/ClaudeMcpServer.Infrastructure/Tools/MyTool.cs`:

```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;

public sealed class MyTool : IToolHandler
{
    public string ToolName => "my_tool";

    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Does something useful.",
        new JsonObject { ["type"] = "object", ["properties"] = new JsonObject() });

    public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
        => Task.FromResult(ToolResult.Success("Hello from MyTool!"));
}
```

**Step 2** — Register in `src/ClaudeMcpServer.Host/Program.cs`:

```csharp
services.AddSingleton<IToolHandler, MyTool>();
```

Done. Rebuild and restart Claude Desktop.

---

## Project Structure

```
ClaudeMcpServer/
├── src/
│   ├── ClaudeMcpServer.Domain/           # Interfaces, entities, value objects
│   ├── ClaudeMcpServer.Application/      # Handlers, McpService, DTOs
│   ├── ClaudeMcpServer.Infrastructure/   # Tools, transport, registry, LicenseService
│   ├── ClaudeMcpServer.Host/             # Entry point, DI wiring, appsettings
│   └── ClaudeMcpServer.LicenseServer/    # Standalone license API (Railway)
│       ├── Data/                         # EF Core DbContext
│       ├── DTOs/                         # Request/response models
│       ├── Filters/                      # AdminKeyFilter (endpoint security)
│       ├── Migrations/                   # EF Core database migrations
│       ├── Models/                       # LicenseKey, SessionToken entities
│       ├── Repositories/                 # IRepository<T>, Repository interfaces + UnitOfWork
│       ├── Services/                     # ILicenseManagerService, LicenseManagerService, LoggingLicenseManagerService
│       ├── Dockerfile                    # Container build for Railway
│       └── Program.cs                    # Minimal API endpoints
├── tests/
│   ├── ClaudeMcpServer.Domain.Tests/
│   ├── ClaudeMcpServer.Application.Tests/
│   └── ClaudeMcpServer.Infrastructure.Tests/
├── railway.json                          # Railway deployment config
└── ClaudeMcpServer.sln
```

---

## License Server — Required Environment Variables (Railway)

| Variable | Description |
|---|---|
| `AdminKey` | Secret key for admin endpoints (`X-Admin-Key` header) |
| `PORT` | HTTP port (Railway sets this automatically) |
| `ConnectionStrings__DefaultConnection` | SQLite path, e.g. `Data Source=/app/data/licenses.db` |

---

## License

[MIT](LICENSE)
