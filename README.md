# ClaudeMcpServer

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)](#quick-start)

A production-ready **Model Context Protocol (MCP) server** written in C# .NET 10.  
Connects [Claude Desktop](https://claude.ai/download) on macOS to a set of local tools via the MCP stdio transport.

---

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                      ClaudeMcpServer                         │
│                                                              │
│  ┌──────────┐    ┌─────────────────┐    ┌─────────────────┐ │
│  │  Domain  │◄───│  Application    │◄───│ Infrastructure  │ │
│  │          │    │                 │    │                 │ │
│  │Interfaces│    │  McpService     │    │ StdioTransport  │ │
│  │Entities  │    │  Handlers:      │    │ ToolRegistry    │ │
│  │ValueObjs │    │  - Initialize   │    │ Tools:          │ │
│  └──────────┘    │  - ListTools    │    │ - SystemInfo    │ │
│                  │  - CallTool     │    │ - DateTime      │ │
│                  │  - Ping         │    │ - ReadFile      │ │
│                  └─────────────────┘    │ - ShellCommand  │ │
│                                         │ - ListDirectory │ │
│  ┌──────────────────────────────────┐   └─────────────────┘ │
│  │             Host                 │                        │
│  │  Program.cs + Generic Host       │                        │
│  └──────────────────────────────────┘                        │
└─────────────────────────────────────────────────────────────┘
              │ stdio (JSON-RPC 2.0)
              ▼
        Claude Desktop
```

**Extension point:** Add a new tool by creating one class implementing `IToolHandler` and registering it in DI. Zero changes to the protocol layer.

---

## Built-in Tools

### System Tools

| Tool | Description |
|------|-------------|
| `get_system_info` | OS, CPU architecture, .NET version, hostname, working set |
| `get_datetime` | Current date/time in multiple formats with optional IANA timezone |
| `read_file` | Read text/code files up to 1 MB (whitelisted extensions) |
| `run_shell_command` | Execute whitelisted commands: `date`, `echo`, `ls`, `pwd`, `uname`, `whoami` |
| `list_directory` | List directory contents with type, size, and modification date |

### Email Tools (iCloud / me.com)

| Tool | Description |
|------|-------------|
| `list_emails` | List recent emails from the iCloud inbox (sender, subject, date, preview) |
| `read_email` | Read the full content of an email by its unique ID |
| `search_emails` | Search inbox by subject, sender, body text, or all fields |
| `send_email` | Send an email from the configured iCloud account |

> **Setup:** Email tools require an [app-specific password](https://support.apple.com/en-us/102654) generated at appleid.apple.com. Set it in `appsettings.json` under `Email.Password`.

---

## Quick Start (macOS)

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- [Claude Desktop](https://claude.ai/download) for macOS

### 1. Clone and build

```bash
git clone https://github.com/YOUR_USERNAME/ClaudeMcpServer.git
cd ClaudeMcpServer
dotnet build ClaudeMcpServer.sln
```

### 2. Run tests

```bash
dotnet test ClaudeMcpServer.sln
```

### 3. Smoke-test the server

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"ping","params":{}}' | dotnet run --project src/ClaudeMcpServer.Host/
```

Expected stdout: `{"jsonrpc":"2.0","id":1,"result":{}}`

### 4. Publish a self-contained binary

```bash
# Apple Silicon (M1/M2/M3)
dotnet publish src/ClaudeMcpServer.Host/ -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64

# Intel Mac
dotnet publish src/ClaudeMcpServer.Host/ -c Release -r osx-x64 --self-contained -o ./publish/osx-x64
```

### 5. Configure Claude Desktop

Edit `~/Library/Application Support/Claude/claude_desktop_config.json`:

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
│   ├── ClaudeMcpServer.Domain/          # Interfaces, entities, value objects
│   ├── ClaudeMcpServer.Application/     # Handlers, McpService, DTOs
│   ├── ClaudeMcpServer.Infrastructure/  # Tools, transport, registry
│   └── ClaudeMcpServer.Host/            # Entry point, DI wiring
└── tests/
    ├── ClaudeMcpServer.Application.Tests/
    └── ClaudeMcpServer.Infrastructure.Tests/
```

---

## License

[MIT](LICENSE)
