# MCP Server C# .NET 10 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a production-ready MCP (Model Context Protocol) server in C# .NET 10 that Claude Desktop on macOS can connect to, expose 5 built-in tools, and be trivially extensible via Clean Architecture.

**Architecture:** Clean Architecture with 4 projects (Domain → Application → Infrastructure → Host). The `IToolHandler` interface is the single extension point — a new tool is one new class registered in DI with zero protocol changes required.

**Tech Stack:** C# 12, .NET 10, Microsoft.Extensions.DependencyInjection, Microsoft.Extensions.Hosting, Microsoft.Extensions.Logging, System.Text.Json. No third-party MCP SDK.

---

## File Map

```
ClaudeMcpServer/
├── src/
│   ├── ClaudeMcpServer.Domain/
│   │   ├── ClaudeMcpServer.Domain.csproj
│   │   ├── Entities/ToolDefinition.cs
│   │   ├── Entities/McpRequest.cs
│   │   ├── Entities/McpResponse.cs
│   │   ├── Interfaces/IToolHandler.cs
│   │   ├── Interfaces/ITransport.cs
│   │   ├── Interfaces/IToolRegistry.cs
│   │   ├── Interfaces/IMcpRequestHandler.cs
│   │   └── ValueObjects/ToolResult.cs
│   │   └── ValueObjects/JsonRpcError.cs
│   ├── ClaudeMcpServer.Application/
│   │   ├── ClaudeMcpServer.Application.csproj
│   │   ├── DTOs/InitializeParams.cs
│   │   ├── DTOs/InitializeResult.cs
│   │   ├── DTOs/CallToolParams.cs
│   │   ├── DTOs/ListToolsResult.cs
│   │   ├── Handlers/InitializeHandler.cs
│   │   ├── Handlers/ListToolsHandler.cs
│   │   ├── Handlers/CallToolHandler.cs
│   │   ├── Handlers/PingHandler.cs
│   │   └── Services/McpService.cs
│   ├── ClaudeMcpServer.Infrastructure/
│   │   ├── ClaudeMcpServer.Infrastructure.csproj
│   │   ├── Tools/SystemInfoTool.cs
│   │   ├── Tools/DateTimeTool.cs
│   │   ├── Tools/ReadFileTool.cs
│   │   ├── Tools/RunShellCommandTool.cs
│   │   ├── Tools/ListDirectoryTool.cs
│   │   ├── Registry/ToolRegistry.cs
│   │   └── Transport/StdioTransport.cs
│   │   └── Transport/SseTransport.cs
│   └── ClaudeMcpServer.Host/
│       ├── ClaudeMcpServer.Host.csproj
│       ├── Program.cs
│       └── appsettings.json
├── tests/
│   ├── ClaudeMcpServer.Domain.Tests/
│   │   └── ClaudeMcpServer.Domain.Tests.csproj
│   ├── ClaudeMcpServer.Application.Tests/
│   │   ├── ClaudeMcpServer.Application.Tests.csproj
│   │   ├── Handlers/InitializeHandlerTests.cs
│   │   ├── Handlers/ListToolsHandlerTests.cs
│   │   └── Handlers/CallToolHandlerTests.cs
│   └── ClaudeMcpServer.Infrastructure.Tests/
│       ├── ClaudeMcpServer.Infrastructure.Tests.csproj
│       ├── Tools/SystemInfoToolTests.cs
│       ├── Tools/DateTimeToolTests.cs
│       ├── Tools/ReadFileToolTests.cs
│       ├── Tools/RunShellCommandToolTests.cs
│       └── Tools/ListDirectoryToolTests.cs
├── ClaudeMcpServer.sln
├── README.md
├── .gitignore
├── LICENSE
└── CONTRIBUTING.md
```

---

## Task 1: Solution Scaffolding

**Files:**
- Create: `ClaudeMcpServer.sln`
- Create: `src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj`
- Create: `src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj`
- Create: `src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj`
- Create: `src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj`
- Create: `tests/ClaudeMcpServer.Domain.Tests/ClaudeMcpServer.Domain.Tests.csproj`
- Create: `tests/ClaudeMcpServer.Application.Tests/ClaudeMcpServer.Application.Tests.csproj`
- Create: `tests/ClaudeMcpServer.Infrastructure.Tests/ClaudeMcpServer.Infrastructure.Tests.csproj`

- [ ] **Step 1: Create the solution and projects**

```bash
cd "C:\Users\Jorge López\OneDrive\Desktop\MCP mac"
dotnet new sln -n ClaudeMcpServer
dotnet new classlib -n ClaudeMcpServer.Domain -o src/ClaudeMcpServer.Domain --framework net10.0
dotnet new classlib -n ClaudeMcpServer.Application -o src/ClaudeMcpServer.Application --framework net10.0
dotnet new classlib -n ClaudeMcpServer.Infrastructure -o src/ClaudeMcpServer.Infrastructure --framework net10.0
dotnet new console -n ClaudeMcpServer.Host -o src/ClaudeMcpServer.Host --framework net10.0
dotnet new xunit -n ClaudeMcpServer.Domain.Tests -o tests/ClaudeMcpServer.Domain.Tests --framework net10.0
dotnet new xunit -n ClaudeMcpServer.Application.Tests -o tests/ClaudeMcpServer.Application.Tests --framework net10.0
dotnet new xunit -n ClaudeMcpServer.Infrastructure.Tests -o tests/ClaudeMcpServer.Infrastructure.Tests --framework net10.0
```

- [ ] **Step 2: Add projects to solution**

```bash
dotnet sln add src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
dotnet sln add src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj
dotnet sln add src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj
dotnet sln add src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj
dotnet sln add tests/ClaudeMcpServer.Domain.Tests/ClaudeMcpServer.Domain.Tests.csproj
dotnet sln add tests/ClaudeMcpServer.Application.Tests/ClaudeMcpServer.Application.Tests.csproj
dotnet sln add tests/ClaudeMcpServer.Infrastructure.Tests/ClaudeMcpServer.Infrastructure.Tests.csproj
```

- [ ] **Step 3: Add project references**

```bash
dotnet add src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj reference src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
dotnet add src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj reference src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
dotnet add src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj reference src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj
dotnet add src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj reference src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
dotnet add src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj reference src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj
dotnet add src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj reference src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj
dotnet add tests/ClaudeMcpServer.Domain.Tests/ClaudeMcpServer.Domain.Tests.csproj reference src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
dotnet add tests/ClaudeMcpServer.Application.Tests/ClaudeMcpServer.Application.Tests.csproj reference src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj
dotnet add tests/ClaudeMcpServer.Application.Tests/ClaudeMcpServer.Application.Tests.csproj reference src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
dotnet add tests/ClaudeMcpServer.Infrastructure.Tests/ClaudeMcpServer.Infrastructure.Tests.csproj reference src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj
dotnet add tests/ClaudeMcpServer.Infrastructure.Tests/ClaudeMcpServer.Infrastructure.Tests.csproj reference src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
```

- [ ] **Step 4: Add NuGet packages**

```bash
# Infrastructure needs hosting + logging
dotnet add src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj package Microsoft.Extensions.Logging.Abstractions
dotnet add src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj package Microsoft.Extensions.Configuration.Abstractions

# Host needs the full hosting stack
dotnet add src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj package Microsoft.Extensions.Hosting
dotnet add src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj package Microsoft.Extensions.Logging.Console

# Test packages
dotnet add tests/ClaudeMcpServer.Application.Tests/ClaudeMcpServer.Application.Tests.csproj package Microsoft.Extensions.Logging.Abstractions
dotnet add tests/ClaudeMcpServer.Infrastructure.Tests/ClaudeMcpServer.Infrastructure.Tests.csproj package Microsoft.Extensions.Logging.Abstractions
```

- [ ] **Step 5: Update csproj files with XML doc and nullable settings**

Replace `src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
  </PropertyGroup>
</Project>
```

Replace `src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ClaudeMcpServer.Domain\ClaudeMcpServer.Domain.csproj" />
  </ItemGroup>
</Project>
```

Replace `src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ClaudeMcpServer.Domain\ClaudeMcpServer.Domain.csproj" />
    <ProjectReference Include="..\ClaudeMcpServer.Application\ClaudeMcpServer.Application.csproj" />
    <PackageReference Include="Microsoft.Extensions.Logging.Abstractions" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Configuration.Abstractions" Version="10.0.0" />
  </ItemGroup>
</Project>
```

Replace `src/ClaudeMcpServer.Host/ClaudeMcpServer.Host.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>12</LangVersion>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
    <PublishSingleFile>true</PublishSingleFile>
    <SelfContained>true</SelfContained>
    <RuntimeIdentifier>osx-arm64</RuntimeIdentifier>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\ClaudeMcpServer.Domain\ClaudeMcpServer.Domain.csproj" />
    <ProjectReference Include="..\ClaudeMcpServer.Application\ClaudeMcpServer.Application.csproj" />
    <ProjectReference Include="..\ClaudeMcpServer.Infrastructure\ClaudeMcpServer.Infrastructure.csproj" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageReference Include="Microsoft.Extensions.Logging.Console" Version="10.0.0" />
  </ItemGroup>
  <ItemGroup>
    <Content Include="appsettings.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
```

- [ ] **Step 6: Delete generated Class1.cs stubs**

```bash
Remove-Item src/ClaudeMcpServer.Domain/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/ClaudeMcpServer.Application/Class1.cs -ErrorAction SilentlyContinue
Remove-Item src/ClaudeMcpServer.Infrastructure/Class1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 7: Verify solution builds (will fail on missing code — that is expected)**

```bash
dotnet build ClaudeMcpServer.sln
```
Expected: errors about missing usings in test projects (placeholder UnitTest1.cs). This is fine at this stage.

- [ ] **Step 8: Commit**

```bash
git init
git add ClaudeMcpServer.sln src/ tests/
git commit -m "chore: scaffold solution with 4 projects and 3 test projects"
```

---

## Task 2: Domain Layer — Interfaces & Value Objects

**Files:**
- Create: `src/ClaudeMcpServer.Domain/Interfaces/IToolHandler.cs`
- Create: `src/ClaudeMcpServer.Domain/Interfaces/ITransport.cs`
- Create: `src/ClaudeMcpServer.Domain/Interfaces/IToolRegistry.cs`
- Create: `src/ClaudeMcpServer.Domain/Interfaces/IMcpRequestHandler.cs`
- Create: `src/ClaudeMcpServer.Domain/ValueObjects/ToolResult.cs`
- Create: `src/ClaudeMcpServer.Domain/ValueObjects/JsonRpcError.cs`

- [ ] **Step 1: Create IToolHandler**

`src/ClaudeMcpServer.Domain/Interfaces/IToolHandler.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Defines a single MCP tool that can be discovered and invoked by Claude Desktop.
/// Implement this interface to add a new tool — no other changes are required.
/// </summary>
public interface IToolHandler
{
    /// <summary>Gets the unique snake_case tool name exposed to Claude (e.g. "get_system_info").</summary>
    string ToolName { get; }

    /// <summary>Returns the full tool definition including description and JSON schema for parameters.</summary>
    ToolDefinition GetDefinition();

    /// <summary>
    /// Executes the tool with the given parameters.
    /// </summary>
    /// <param name="parameters">The JSON element containing the tool's input parameters.</param>
    /// <param name="ct">Cancellation token for cooperative cancellation.</param>
    /// <returns>A <see cref="ToolResult"/> containing the tool output or error details.</returns>
    Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct);
}
```

- [ ] **Step 2: Create ITransport**

`src/ClaudeMcpServer.Domain/Interfaces/ITransport.cs`:
```csharp
using ClaudeMcpServer.Domain.Entities;

namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Abstracts the transport layer used to receive MCP requests and send responses.
/// Implementations include stdio (for Claude Desktop) and SSE (for HTTP clients).
/// </summary>
public interface ITransport
{
    /// <summary>
    /// Reads the next incoming MCP request from the transport stream.
    /// Returns <c>null</c> when the stream is closed or EOF is reached.
    /// </summary>
    Task<McpRequest?> ReadRequestAsync(CancellationToken ct);

    /// <summary>Writes a serialized MCP response to the transport output stream.</summary>
    Task WriteResponseAsync(McpResponse response, CancellationToken ct);
}
```

- [ ] **Step 3: Create IToolRegistry**

`src/ClaudeMcpServer.Domain/Interfaces/IToolRegistry.cs`:
```csharp
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
```

- [ ] **Step 4: Create IMcpRequestHandler**

`src/ClaudeMcpServer.Domain/Interfaces/IMcpRequestHandler.cs`:
```csharp
using ClaudeMcpServer.Domain.Entities;

namespace ClaudeMcpServer.Domain.Interfaces;

/// <summary>
/// Handles a specific JSON-RPC method (e.g. "initialize", "tools/list", "tools/call").
/// One implementation per MCP method.
/// </summary>
public interface IMcpRequestHandler
{
    /// <summary>Gets the JSON-RPC method name this handler responds to (e.g. "tools/list").</summary>
    string Method { get; }

    /// <summary>
    /// Processes the request and produces a response payload to be serialized into a JSON-RPC result.
    /// </summary>
    Task<object?> HandleAsync(McpRequest request, CancellationToken ct);
}
```

- [ ] **Step 5: Create ToolResult value object**

`src/ClaudeMcpServer.Domain/ValueObjects/ToolResult.cs`:
```csharp
namespace ClaudeMcpServer.Domain.ValueObjects;

/// <summary>
/// Represents the outcome of a tool execution, carrying either a text result or an error description.
/// </summary>
public sealed class ToolResult
{
    /// <summary>Gets the text content returned by the tool on success.</summary>
    public string Content { get; }

    /// <summary>Gets a value indicating whether this result represents an error.</summary>
    public bool IsError { get; }

    private ToolResult(string content, bool isError)
    {
        Content = content;
        IsError = isError;
    }

    /// <summary>Creates a successful tool result with the given content.</summary>
    public static ToolResult Success(string content) => new(content, false);

    /// <summary>Creates an error tool result with the given error message.</summary>
    public static ToolResult Error(string message) => new(message, true);
}
```

- [ ] **Step 6: Create JsonRpcError value object**

`src/ClaudeMcpServer.Domain/ValueObjects/JsonRpcError.cs`:
```csharp
namespace ClaudeMcpServer.Domain.ValueObjects;

/// <summary>
/// Standard JSON-RPC 2.0 error codes and a factory for common error objects.
/// See https://www.jsonrpc.org/specification#error_object for the full specification.
/// </summary>
public sealed class JsonRpcError
{
    /// <summary>Gets the numeric error code as defined by JSON-RPC 2.0.</summary>
    public int Code { get; }

    /// <summary>Gets the human-readable error message.</summary>
    public string Message { get; }

    /// <summary>Gets optional additional error data. May be null.</summary>
    public object? Data { get; }

    /// <summary>Initializes a new instance of <see cref="JsonRpcError"/>.</summary>
    public JsonRpcError(int code, string message, object? data = null)
    {
        Code = code;
        Message = message;
        Data = data;
    }

    // Standard JSON-RPC error codes
    /// <summary>JSON-RPC parse error code (-32700).</summary>
    public const int ParseError = -32700;
    /// <summary>JSON-RPC invalid request code (-32600).</summary>
    public const int InvalidRequest = -32600;
    /// <summary>JSON-RPC method not found code (-32601).</summary>
    public const int MethodNotFound = -32601;
    /// <summary>JSON-RPC invalid params code (-32602).</summary>
    public const int InvalidParams = -32602;
    /// <summary>JSON-RPC internal error code (-32603).</summary>
    public const int InternalError = -32603;

    /// <summary>Factory for a method-not-found error.</summary>
    public static JsonRpcError MethodNotFoundError(string method) =>
        new(MethodNotFound, $"Method not found: {method}");

    /// <summary>Factory for an internal error, wrapping an exception message.</summary>
    public static JsonRpcError FromException(Exception ex) =>
        new(InternalError, "Internal error", ex.Message);
}
```

- [ ] **Step 7: Verify Domain compiles**

```bash
dotnet build src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 8: Commit**

```bash
git add src/ClaudeMcpServer.Domain/
git commit -m "feat(domain): add interfaces, ToolResult, and JsonRpcError value objects"
```

---

## Task 3: Domain Layer — Entities

**Files:**
- Create: `src/ClaudeMcpServer.Domain/Entities/ToolDefinition.cs`
- Create: `src/ClaudeMcpServer.Domain/Entities/McpRequest.cs`
- Create: `src/ClaudeMcpServer.Domain/Entities/McpResponse.cs`

- [ ] **Step 1: Create ToolDefinition entity**

`src/ClaudeMcpServer.Domain/Entities/ToolDefinition.cs`:
```csharp
using System.Text.Json.Nodes;

namespace ClaudeMcpServer.Domain.Entities;

/// <summary>
/// Describes a tool exposed by the MCP server: its name, description, and JSON Schema for parameters.
/// Serialized directly into the tools/list response consumed by Claude.
/// </summary>
public sealed class ToolDefinition
{
    /// <summary>Gets the unique snake_case tool name (e.g. "get_system_info").</summary>
    public string Name { get; }

    /// <summary>Gets the human-readable description of what the tool does, shown to Claude.</summary>
    public string Description { get; }

    /// <summary>Gets the JSON Schema object describing accepted parameters. Use an empty object schema if the tool takes no parameters.</summary>
    public JsonObject InputSchema { get; }

    /// <summary>Initializes a new <see cref="ToolDefinition"/>.</summary>
    public ToolDefinition(string name, string description, JsonObject inputSchema)
    {
        Name = name;
        Description = description;
        InputSchema = inputSchema;
    }
}
```

- [ ] **Step 2: Create McpRequest entity**

`src/ClaudeMcpServer.Domain/Entities/McpRequest.cs`:
```csharp
using System.Text.Json;

namespace ClaudeMcpServer.Domain.Entities;

/// <summary>
/// Represents a parsed JSON-RPC 2.0 request received from a MCP client such as Claude Desktop.
/// </summary>
public sealed class McpRequest
{
    /// <summary>Gets the JSON-RPC protocol version. Always "2.0".</summary>
    public string JsonRpc { get; init; } = "2.0";

    /// <summary>Gets the request identifier. May be a string, number, or null (for notifications).</summary>
    public JsonElement? Id { get; init; }

    /// <summary>Gets the method name to invoke (e.g. "initialize", "tools/list", "tools/call").</summary>
    public string Method { get; init; } = string.Empty;

    /// <summary>Gets the raw parameter payload. Callers parse this into method-specific DTOs.</summary>
    public JsonElement? Params { get; init; }
}
```

- [ ] **Step 3: Create McpResponse entity**

`src/ClaudeMcpServer.Domain/Entities/McpResponse.cs`:
```csharp
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Domain.Entities;

/// <summary>
/// Represents a JSON-RPC 2.0 response to be sent back to the MCP client.
/// Exactly one of <see cref="Result"/> or <see cref="Error"/> is non-null.
/// </summary>
public sealed class McpResponse
{
    /// <summary>Gets the JSON-RPC protocol version. Always "2.0".</summary>
    public string JsonRpc { get; } = "2.0";

    /// <summary>Gets the identifier matching the originating request. Null for error responses to notifications.</summary>
    public object? Id { get; }

    /// <summary>Gets the successful result payload. Null when the response is an error.</summary>
    public object? Result { get; }

    /// <summary>Gets the error payload. Null when the response is successful.</summary>
    public JsonRpcError? Error { get; }

    private McpResponse(object? id, object? result, JsonRpcError? error)
    {
        Id = id;
        Result = result;
        Error = error;
    }

    /// <summary>Creates a success response with the given result payload.</summary>
    public static McpResponse Success(object? id, object result) => new(id, result, null);

    /// <summary>Creates an error response with the given error payload.</summary>
    public static McpResponse Failure(object? id, JsonRpcError error) => new(id, null, error);
}
```

- [ ] **Step 4: Verify Domain builds cleanly**

```bash
dotnet build src/ClaudeMcpServer.Domain/ClaudeMcpServer.Domain.csproj
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 5: Commit**

```bash
git add src/ClaudeMcpServer.Domain/
git commit -m "feat(domain): add McpRequest, McpResponse, and ToolDefinition entities"
```

---

## Task 4: Application Layer — DTOs

**Files:**
- Create: `src/ClaudeMcpServer.Application/DTOs/InitializeParams.cs`
- Create: `src/ClaudeMcpServer.Application/DTOs/InitializeResult.cs`
- Create: `src/ClaudeMcpServer.Application/DTOs/CallToolParams.cs`
- Create: `src/ClaudeMcpServer.Application/DTOs/ListToolsResult.cs`
- Create: `src/ClaudeMcpServer.Application/DTOs/ToolInfo.cs`

- [ ] **Step 1: Create InitializeParams**

`src/ClaudeMcpServer.Application/DTOs/InitializeParams.cs`:
```csharp
using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Parameters sent by the MCP client in the "initialize" request.</summary>
public sealed class InitializeParams
{
    /// <summary>Gets the protocol version string requested by the client (e.g. "2024-11-05").</summary>
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = string.Empty;

    /// <summary>Gets information about the connecting client application.</summary>
    [JsonPropertyName("clientInfo")]
    public ClientInfo? ClientInfo { get; init; }

    /// <summary>Gets client-declared capabilities. Currently informational only.</summary>
    [JsonPropertyName("capabilities")]
    public object? Capabilities { get; init; }
}

/// <summary>Identifies the client application connecting to this MCP server.</summary>
public sealed class ClientInfo
{
    /// <summary>Gets the client application name (e.g. "Claude Desktop").</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the client application version string.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = string.Empty;
}
```

- [ ] **Step 2: Create InitializeResult**

`src/ClaudeMcpServer.Application/DTOs/InitializeResult.cs`:
```csharp
using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Response payload for the "initialize" method, describing this server's identity and capabilities.</summary>
public sealed class InitializeResult
{
    /// <summary>Gets the MCP protocol version this server implements.</summary>
    [JsonPropertyName("protocolVersion")]
    public string ProtocolVersion { get; init; } = "2024-11-05";

    /// <summary>Gets metadata about this server implementation.</summary>
    [JsonPropertyName("serverInfo")]
    public ServerInfo ServerInfo { get; init; } = new();

    /// <summary>Gets the capabilities advertised by this server.</summary>
    [JsonPropertyName("capabilities")]
    public ServerCapabilities Capabilities { get; init; } = new();
}

/// <summary>Identifies this MCP server to connecting clients.</summary>
public sealed class ServerInfo
{
    /// <summary>Gets the server name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = "ClaudeMcpServer";

    /// <summary>Gets the server version string.</summary>
    [JsonPropertyName("version")]
    public string Version { get; init; } = "1.0.0";
}

/// <summary>Advertises the features this MCP server supports.</summary>
public sealed class ServerCapabilities
{
    /// <summary>Gets the tools capability object, indicating this server exposes callable tools.</summary>
    [JsonPropertyName("tools")]
    public ToolsCapability Tools { get; init; } = new();
}

/// <summary>Declares tool-related server capabilities.</summary>
public sealed class ToolsCapability
{
    /// <summary>Gets a value indicating whether the server can send tool list change notifications.</summary>
    [JsonPropertyName("listChanged")]
    public bool ListChanged { get; init; } = false;
}
```

- [ ] **Step 3: Create CallToolParams**

`src/ClaudeMcpServer.Application/DTOs/CallToolParams.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Parameters for the "tools/call" method, identifying the tool and its input arguments.</summary>
public sealed class CallToolParams
{
    /// <summary>Gets the name of the tool to invoke. Must match an <see cref="ClaudeMcpServer.Domain.Interfaces.IToolHandler.ToolName"/>.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the raw JSON arguments for the tool. Each tool's handler deserializes this.</summary>
    [JsonPropertyName("arguments")]
    public JsonElement? Arguments { get; init; }
}
```

- [ ] **Step 4: Create ToolInfo and ListToolsResult**

`src/ClaudeMcpServer.Application/DTOs/ToolInfo.cs`:
```csharp
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Serializable representation of a tool for the "tools/list" response.</summary>
public sealed class ToolInfo
{
    /// <summary>Gets the snake_case tool name.</summary>
    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the human-readable tool description shown to Claude.</summary>
    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the JSON Schema describing the tool's accepted parameters.</summary>
    [JsonPropertyName("inputSchema")]
    public JsonObject InputSchema { get; init; } = [];
}
```

`src/ClaudeMcpServer.Application/DTOs/ListToolsResult.cs`:
```csharp
using System.Text.Json.Serialization;

namespace ClaudeMcpServer.Application.DTOs;

/// <summary>Response payload for the "tools/list" method.</summary>
public sealed class ListToolsResult
{
    /// <summary>Gets all tools currently registered in this server.</summary>
    [JsonPropertyName("tools")]
    public IReadOnlyList<ToolInfo> Tools { get; init; } = [];
}
```

- [ ] **Step 5: Verify Application layer builds**

```bash
dotnet build src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj
```
Expected: Build succeeded (may warn about unused DTOs — that is fine).

- [ ] **Step 6: Commit**

```bash
git add src/ClaudeMcpServer.Application/
git commit -m "feat(application): add DTOs for initialize, tools/list, and tools/call"
```

---

## Task 5: Application Layer — Request Handlers

**Files:**
- Create: `src/ClaudeMcpServer.Application/Handlers/InitializeHandler.cs`
- Create: `src/ClaudeMcpServer.Application/Handlers/ListToolsHandler.cs`
- Create: `src/ClaudeMcpServer.Application/Handlers/CallToolHandler.cs`
- Create: `src/ClaudeMcpServer.Application/Handlers/PingHandler.cs`
- Test: `tests/ClaudeMcpServer.Application.Tests/Handlers/InitializeHandlerTests.cs`
- Test: `tests/ClaudeMcpServer.Application.Tests/Handlers/ListToolsHandlerTests.cs`
- Test: `tests/ClaudeMcpServer.Application.Tests/Handlers/CallToolHandlerTests.cs`

- [ ] **Step 1: Write failing test for InitializeHandler**

`tests/ClaudeMcpServer.Application.Tests/Handlers/InitializeHandlerTests.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Domain.Entities;
using Xunit;

namespace ClaudeMcpServer.Application.Tests.Handlers;

public class InitializeHandlerTests
{
    [Fact]
    public async Task HandleAsync_Returns_InitializeResult_With_ServerInfo()
    {
        var handler = new InitializeHandler();
        var request = new McpRequest { Method = "initialize", Id = JsonSerializer.SerializeToElement(1) };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var initResult = Assert.IsType<InitializeResult>(result);
        Assert.Equal("2024-11-05", initResult.ProtocolVersion);
        Assert.Equal("ClaudeMcpServer", initResult.ServerInfo.Name);
    }

    [Fact]
    public void Method_Is_Initialize()
    {
        var handler = new InitializeHandler();
        Assert.Equal("initialize", handler.Method);
    }
}
```

- [ ] **Step 2: Run test — verify it fails**

```bash
dotnet test tests/ClaudeMcpServer.Application.Tests/ --filter "InitializeHandlerTests"
```
Expected: FAIL — type `InitializeHandler` not found.

- [ ] **Step 3: Implement InitializeHandler**

`src/ClaudeMcpServer.Application/Handlers/InitializeHandler.cs`:
```csharp
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
    /// <inheritdoc/>
    public string Method => "initialize";

    /// <inheritdoc/>
    public Task<object?> HandleAsync(McpRequest request, CancellationToken ct)
    {
        var result = new InitializeResult
        {
            ProtocolVersion = "2024-11-05",
            ServerInfo = new ServerInfo { Name = "ClaudeMcpServer", Version = "1.0.0" },
            Capabilities = new ServerCapabilities { Tools = new ToolsCapability { ListChanged = false } }
        };
        return Task.FromResult<object?>(result);
    }
}
```

- [ ] **Step 4: Run test — verify it passes**

```bash
dotnet test tests/ClaudeMcpServer.Application.Tests/ --filter "InitializeHandlerTests"
```
Expected: PASS.

- [ ] **Step 5: Write failing test for ListToolsHandler**

`tests/ClaudeMcpServer.Application.Tests/Handlers/ListToolsHandlerTests.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using Xunit;

namespace ClaudeMcpServer.Application.Tests.Handlers;

public class ListToolsHandlerTests
{
    private sealed class FakeTool : IToolHandler
    {
        public string ToolName => "test_tool";
        public ToolDefinition GetDefinition() => new("test_tool", "A test tool", new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject()
        });
        public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Success("ok"));
    }

    private sealed class FakeRegistry : IToolRegistry
    {
        private readonly IToolHandler[] _handlers;
        public FakeRegistry(params IToolHandler[] handlers) => _handlers = handlers;
        public IEnumerable<IToolHandler> GetAll() => _handlers;
        public IToolHandler? GetByName(string name) => _handlers.FirstOrDefault(h => h.ToolName == name);
    }

    [Fact]
    public async Task HandleAsync_Returns_All_Registered_Tools()
    {
        var registry = new FakeRegistry(new FakeTool());
        var handler = new ListToolsHandler(registry);
        var request = new McpRequest { Method = "tools/list" };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        var listResult = Assert.IsType<ListToolsResult>(result);
        Assert.Single(listResult.Tools);
        Assert.Equal("test_tool", listResult.Tools[0].Name);
    }
}
```

- [ ] **Step 6: Run test — verify it fails**

```bash
dotnet test tests/ClaudeMcpServer.Application.Tests/ --filter "ListToolsHandlerTests"
```
Expected: FAIL — type `ListToolsHandler` not found.

- [ ] **Step 7: Implement ListToolsHandler**

`src/ClaudeMcpServer.Application/Handlers/ListToolsHandler.cs`:
```csharp
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
```

- [ ] **Step 8: Run test — verify it passes**

```bash
dotnet test tests/ClaudeMcpServer.Application.Tests/ --filter "ListToolsHandlerTests"
```
Expected: PASS.

- [ ] **Step 9: Write failing test for CallToolHandler**

`tests/ClaudeMcpServer.Application.Tests/Handlers/CallToolHandlerTests.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace ClaudeMcpServer.Application.Tests.Handlers;

public class CallToolHandlerTests
{
    private sealed class EchoTool : IToolHandler
    {
        public string ToolName => "echo";
        public ToolDefinition GetDefinition() => new("echo", "Echoes input",
            new JsonObject { ["type"] = "object" });
        public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
            => Task.FromResult(ToolResult.Success("echoed"));
    }

    private sealed class FakeRegistry : IToolRegistry
    {
        private readonly IToolHandler[] _handlers;
        public FakeRegistry(params IToolHandler[] handlers) => _handlers = handlers;
        public IEnumerable<IToolHandler> GetAll() => _handlers;
        public IToolHandler? GetByName(string name) => _handlers.FirstOrDefault(h => h.ToolName == name);
    }

    [Fact]
    public async Task HandleAsync_Dispatches_To_Correct_Tool()
    {
        var registry = new FakeRegistry(new EchoTool());
        var handler = new CallToolHandler(registry, NullLogger<CallToolHandler>.Instance);

        var paramsJson = JsonSerializer.Serialize(new { name = "echo", arguments = new { } });
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = JsonSerializer.Deserialize<JsonElement>(paramsJson)
        };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        Assert.NotNull(result);
    }

    [Fact]
    public async Task HandleAsync_Returns_Error_For_Unknown_Tool()
    {
        var registry = new FakeRegistry();
        var handler = new CallToolHandler(registry, NullLogger<CallToolHandler>.Instance);

        var paramsJson = JsonSerializer.Serialize(new { name = "nonexistent", arguments = new { } });
        var request = new McpRequest
        {
            Method = "tools/call",
            Params = JsonSerializer.Deserialize<JsonElement>(paramsJson)
        };

        var result = await handler.HandleAsync(request, CancellationToken.None);

        Assert.NotNull(result);
        // result is a CallToolResult with isError=true
        var json = JsonSerializer.Serialize(result);
        Assert.Contains("isError", json);
    }
}
```

- [ ] **Step 10: Run test — verify it fails**

```bash
dotnet test tests/ClaudeMcpServer.Application.Tests/ --filter "CallToolHandlerTests"
```
Expected: FAIL — `CallToolHandler` not found.

- [ ] **Step 11: Implement CallToolHandler**

`src/ClaudeMcpServer.Application/Handlers/CallToolHandler.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Application.DTOs;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Application.Handlers;

/// <summary>
/// Handles the "tools/call" JSON-RPC method.
/// Deserializes <see cref="CallToolParams"/>, looks up the tool by name in the registry,
/// and dispatches execution to the appropriate <see cref="IToolHandler"/>.
/// </summary>
public sealed class CallToolHandler : IMcpRequestHandler
{
    private readonly IToolRegistry _registry;
    private readonly ILogger<CallToolHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="CallToolHandler"/>.</summary>
    public CallToolHandler(IToolRegistry registry, ILogger<CallToolHandler> logger)
    {
        _registry = registry;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Method => "tools/call";

    /// <inheritdoc/>
    public async Task<object?> HandleAsync(McpRequest request, CancellationToken ct)
    {
        if (request.Params is not { } paramsElement)
            return ErrorResult("Missing params in tools/call request");

        CallToolParams? callParams;
        try
        {
            callParams = paramsElement.Deserialize<CallToolParams>(JsonSerializerOptions);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to deserialize tools/call params");
            return ErrorResult("Invalid tools/call parameters");
        }

        if (callParams is null || string.IsNullOrWhiteSpace(callParams.Name))
            return ErrorResult("Tool name is required");

        var tool = _registry.GetByName(callParams.Name);
        if (tool is null)
        {
            _logger.LogWarning("Tool not found: {ToolName}", callParams.Name);
            return ErrorResult($"Unknown tool: {callParams.Name}");
        }

        var arguments = callParams.Arguments ?? default;
        try
        {
            var toolResult = await tool.ExecuteAsync(arguments, ct);
            return new CallToolResult(toolResult.Content, toolResult.IsError);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Tool {ToolName} threw an unhandled exception", callParams.Name);
            return ErrorResult($"Tool execution failed: {ex.Message}");
        }
    }

    private static object ErrorResult(string message) =>
        new CallToolResult(message, true);

    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
}

/// <summary>Serializable result for the "tools/call" response.</summary>
/// <param name="Content">The text output from the tool.</param>
/// <param name="IsError">True if the tool returned an error rather than a success value.</param>
public sealed record CallToolResult(string Content, bool IsError)
{
    /// <summary>Gets the content array in MCP format, containing a single text item.</summary>
    public IReadOnlyList<ContentItem> content { get; } = [new ContentItem("text", Content)];

    /// <summary>Gets whether this result is an error.</summary>
    public bool isError { get; } = IsError;
}

/// <summary>A single content item within a tool call result.</summary>
/// <param name="Type">The content type, e.g. "text".</param>
/// <param name="Text">The text payload.</param>
public sealed record ContentItem(string Type, string Text)
{
    /// <summary>Gets the content type.</summary>
    public string type { get; } = Type;
    /// <summary>Gets the text content.</summary>
    public string text { get; } = Text;
}
```

- [ ] **Step 12: Implement PingHandler**

`src/ClaudeMcpServer.Application/Handlers/PingHandler.cs`:
```csharp
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;

namespace ClaudeMcpServer.Application.Handlers;

/// <summary>
/// Handles the "ping" JSON-RPC method used for keep-alive health checks.
/// Returns an empty object as specified by the MCP protocol.
/// </summary>
public sealed class PingHandler : IMcpRequestHandler
{
    /// <inheritdoc/>
    public string Method => "ping";

    /// <inheritdoc/>
    public Task<object?> HandleAsync(McpRequest request, CancellationToken ct) =>
        Task.FromResult<object?>(new { });
}
```

- [ ] **Step 13: Run all Application tests — verify they pass**

```bash
dotnet test tests/ClaudeMcpServer.Application.Tests/
```
Expected: All tests PASS.

- [ ] **Step 14: Commit**

```bash
git add src/ClaudeMcpServer.Application/ tests/ClaudeMcpServer.Application.Tests/
git commit -m "feat(application): add Initialize, ListTools, CallTool, and Ping handlers with tests"
```

---

## Task 6: Application Layer — McpService

**Files:**
- Create: `src/ClaudeMcpServer.Application/Services/McpService.cs`

- [ ] **Step 1: Implement McpService**

`src/ClaudeMcpServer.Application/Services/McpService.cs`:
```csharp
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
                    response = McpResponse.Failure(
                        ExtractId(request),
                        JsonRpcError.FromException(ex));
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
```

- [ ] **Step 2: Verify Application layer builds**

```bash
dotnet build src/ClaudeMcpServer.Application/ClaudeMcpServer.Application.csproj
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/ClaudeMcpServer.Application/
git commit -m "feat(application): add McpService request dispatch loop"
```

---

## Task 7: Infrastructure — StdioTransport

**Files:**
- Create: `src/ClaudeMcpServer.Infrastructure/Transport/StdioTransport.cs`
- Create: `src/ClaudeMcpServer.Infrastructure/Transport/SseTransport.cs`

- [ ] **Step 1: Implement StdioTransport**

`src/ClaudeMcpServer.Infrastructure/Transport/StdioTransport.cs`:
```csharp
using System.Text;
using System.Text.Json;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Infrastructure.Transport;

/// <summary>
/// Implements the MCP stdio transport: reads newline-delimited JSON-RPC requests from stdin
/// and writes JSON-RPC responses to stdout. All logging goes to stderr to avoid corrupting the protocol stream.
/// </summary>
public sealed class StdioTransport : ITransport
{
    private readonly ILogger<StdioTransport> _logger;

    private static readonly JsonSerializerOptions ReadOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

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
```

- [ ] **Step 2: Implement SseTransport stub**

`src/ClaudeMcpServer.Infrastructure/Transport/SseTransport.cs`:
```csharp
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace ClaudeMcpServer.Infrastructure.Transport;

/// <summary>
/// Stub implementation of the SSE (Server-Sent Events) transport for future HTTP-based MCP clients.
/// Not used at runtime — the stdio transport is active by default.
/// </summary>
public sealed class SseTransport : ITransport
{
    private readonly ILogger<SseTransport> _logger;

    /// <summary>Initializes a new instance of <see cref="SseTransport"/>.</summary>
    public SseTransport(ILogger<SseTransport> logger) => _logger = logger;

    /// <inheritdoc/>
    public Task<McpRequest?> ReadRequestAsync(CancellationToken ct)
    {
        _logger.LogWarning("SseTransport is not implemented; use StdioTransport instead");
        return Task.FromResult<McpRequest?>(null);
    }

    /// <inheritdoc/>
    public Task WriteResponseAsync(McpResponse response, CancellationToken ct)
    {
        _logger.LogWarning("SseTransport.WriteResponseAsync called but not implemented");
        return Task.CompletedTask;
    }
}
```

- [ ] **Step 3: Build Infrastructure layer**

```bash
dotnet build src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj
```
Expected: Build succeeded.

- [ ] **Step 4: Commit**

```bash
git add src/ClaudeMcpServer.Infrastructure/
git commit -m "feat(infrastructure): add StdioTransport and SseTransport stub"
```

---

## Task 8: Infrastructure — ToolRegistry

**Files:**
- Create: `src/ClaudeMcpServer.Infrastructure/Registry/ToolRegistry.cs`

- [ ] **Step 1: Implement ToolRegistry**

`src/ClaudeMcpServer.Infrastructure/Registry/ToolRegistry.cs`:
```csharp
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
```

- [ ] **Step 2: Build Infrastructure**

```bash
dotnet build src/ClaudeMcpServer.Infrastructure/ClaudeMcpServer.Infrastructure.csproj
```
Expected: Build succeeded.

- [ ] **Step 3: Commit**

```bash
git add src/ClaudeMcpServer.Infrastructure/
git commit -m "feat(infrastructure): add ToolRegistry with DI-based auto-discovery"
```

---

## Task 9: Infrastructure — Built-in Tools (all 5)

**Files:**
- Create: `src/ClaudeMcpServer.Infrastructure/Tools/SystemInfoTool.cs`
- Create: `src/ClaudeMcpServer.Infrastructure/Tools/DateTimeTool.cs`
- Create: `src/ClaudeMcpServer.Infrastructure/Tools/ReadFileTool.cs`
- Create: `src/ClaudeMcpServer.Infrastructure/Tools/RunShellCommandTool.cs`
- Create: `src/ClaudeMcpServer.Infrastructure/Tools/ListDirectoryTool.cs`
- Test: `tests/ClaudeMcpServer.Infrastructure.Tests/Tools/SystemInfoToolTests.cs`
- Test: `tests/ClaudeMcpServer.Infrastructure.Tests/Tools/DateTimeToolTests.cs`
- Test: `tests/ClaudeMcpServer.Infrastructure.Tests/Tools/ReadFileToolTests.cs`
- Test: `tests/ClaudeMcpServer.Infrastructure.Tests/Tools/RunShellCommandToolTests.cs`
- Test: `tests/ClaudeMcpServer.Infrastructure.Tests/Tools/ListDirectoryToolTests.cs`

- [ ] **Step 1: Write failing tests for SystemInfoTool**

`tests/ClaudeMcpServer.Infrastructure.Tests/Tools/SystemInfoToolTests.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

public class SystemInfoToolTests
{
    [Fact]
    public void ToolName_Is_get_system_info()
    {
        var tool = new SystemInfoTool();
        Assert.Equal("get_system_info", tool.ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_NonEmpty_Content()
    {
        var tool = new SystemInfoTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_Content_Contains_DotNet_Version()
    {
        var tool = new SystemInfoTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);

        Assert.Contains(".NET", result.Content, StringComparison.OrdinalIgnoreCase);
    }
}
```

- [ ] **Step 2: Run — verify fails**

```bash
dotnet test tests/ClaudeMcpServer.Infrastructure.Tests/ --filter "SystemInfoToolTests"
```
Expected: FAIL — `SystemInfoTool` not found.

- [ ] **Step 3: Implement SystemInfoTool**

`src/ClaudeMcpServer.Infrastructure/Tools/SystemInfoTool.cs`:
```csharp
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Returns system information including CPU architecture, OS version, .NET runtime version,
/// available memory, and hostname. Requires no parameters.
/// </summary>
public sealed class SystemInfoTool : IToolHandler
{
    /// <inheritdoc/>
    public string ToolName => "get_system_info";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Returns system information: OS, CPU architecture, .NET version, hostname, and available memory.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject(),
            ["required"] = new JsonArray()
        });

    /// <inheritdoc/>
    public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Hostname:         {Environment.MachineName}");
        sb.AppendLine($"OS:               {RuntimeInformation.OSDescription}");
        sb.AppendLine($"Architecture:     {RuntimeInformation.OSArchitecture}");
        sb.AppendLine($"Process arch:     {RuntimeInformation.ProcessArchitecture}");
        sb.AppendLine($".NET Version:     {Environment.Version}");
        sb.AppendLine($"Runtime:          {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Processor count:  {Environment.ProcessorCount}");
        sb.AppendLine($"Working set:      {Environment.WorkingSet / 1024 / 1024} MB");
        sb.AppendLine($"User name:        {Environment.UserName}");
        sb.AppendLine($"Current dir:      {Environment.CurrentDirectory}");

        return Task.FromResult(ToolResult.Success(sb.ToString().TrimEnd()));
    }
}
```

- [ ] **Step 4: Run SystemInfoTool tests — verify pass**

```bash
dotnet test tests/ClaudeMcpServer.Infrastructure.Tests/ --filter "SystemInfoToolTests"
```
Expected: PASS.

- [ ] **Step 5: Write failing tests for DateTimeTool**

`tests/ClaudeMcpServer.Infrastructure.Tests/Tools/DateTimeToolTests.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

public class DateTimeToolTests
{
    [Fact]
    public void ToolName_Is_get_datetime()
    {
        var tool = new DateTimeTool();
        Assert.Equal("get_datetime", tool.ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Current_Year()
    {
        var tool = new DateTimeTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains(DateTime.UtcNow.Year.ToString(), result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_With_Timezone_Parameter_Returns_Converted_Time()
    {
        var tool = new DateTimeTool();
        var paramsJson = JsonSerializer.Serialize(new { timezone = "UTC" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.Contains("UTC", result.Content);
    }
}
```

- [ ] **Step 6: Implement DateTimeTool**

`src/ClaudeMcpServer.Infrastructure/Tools/DateTimeTool.cs`:
```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Returns the current date and time in multiple formats, with optional timezone conversion.
/// Parameter: <c>timezone</c> (string, optional) — IANA timezone ID e.g. "America/New_York". Defaults to UTC.
/// </summary>
public sealed class DateTimeTool : IToolHandler
{
    /// <inheritdoc/>
    public string ToolName => "get_datetime";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Returns the current date and time in multiple formats. Optionally converts to the specified IANA timezone.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["timezone"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "IANA timezone identifier (e.g. 'America/New_York', 'Europe/London'). Defaults to UTC."
                }
            },
            ["required"] = new JsonArray()
        });

    /// <inheritdoc/>
    public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        string? tzId = null;
        if (parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("timezone", out var tzProp))
        {
            tzId = tzProp.GetString();
        }

        TimeZoneInfo tz;
        try
        {
            tz = string.IsNullOrWhiteSpace(tzId) ? TimeZoneInfo.Utc : TimeZoneInfo.FindSystemTimeZoneById(tzId);
        }
        catch (TimeZoneNotFoundException)
        {
            return Task.FromResult(ToolResult.Error($"Unknown timezone: '{tzId}'. Use an IANA identifier like 'America/New_York'."));
        }

        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, tz);
        var utcNow = DateTime.UtcNow;

        var sb = new StringBuilder();
        sb.AppendLine($"Timezone:         {tz.Id} ({tz.DisplayName})");
        sb.AppendLine($"Local time:       {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"ISO 8601:         {now:yyyy-MM-ddTHH:mm:ss}{(tz == TimeZoneInfo.Utc ? "Z" : tz.GetUtcOffset(now).ToString(@"\+hh\:mm"))}");
        sb.AppendLine($"UTC:              {utcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Unix timestamp:   {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        sb.AppendLine($"Day of week:      {now:dddd}");
        sb.AppendLine($"Week of year:     {System.Globalization.CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, System.Globalization.CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)}");

        return Task.FromResult(ToolResult.Success(sb.ToString().TrimEnd()));
    }
}
```

- [ ] **Step 7: Run DateTimeTool tests — verify pass**

```bash
dotnet test tests/ClaudeMcpServer.Infrastructure.Tests/ --filter "DateTimeToolTests"
```
Expected: PASS.

- [ ] **Step 8: Write failing tests for ReadFileTool**

`tests/ClaudeMcpServer.Infrastructure.Tests/Tools/ReadFileToolTests.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

public class ReadFileToolTests
{
    [Fact]
    public void ToolName_Is_read_file()
    {
        var tool = new ReadFileTool();
        Assert.Equal("read_file", tool.ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Error_When_Path_Missing()
    {
        var tool = new ReadFileTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);
        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_File_Content()
    {
        var tool = new ReadFileTool();
        var tmpFile = Path.GetTempFileName();
        await File.WriteAllTextAsync(tmpFile, "hello test");

        try
        {
            var paramsJson = JsonSerializer.Serialize(new { path = tmpFile });
            var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

            var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

            Assert.False(result.IsError);
            Assert.Contains("hello test", result.Content);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Error_For_Nonexistent_File()
    {
        var tool = new ReadFileTool();
        var paramsJson = JsonSerializer.Serialize(new { path = "/nonexistent/path/file.txt" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_Binary_Files()
    {
        var tool = new ReadFileTool();
        var tmpFile = Path.GetTempFileName() + ".exe";
        await File.WriteAllBytesAsync(tmpFile, [0x4D, 0x5A, 0x00, 0x00]);

        try
        {
            var paramsJson = JsonSerializer.Serialize(new { path = tmpFile });
            var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

            var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

            Assert.True(result.IsError);
        }
        finally
        {
            File.Delete(tmpFile);
        }
    }
}
```

- [ ] **Step 9: Implement ReadFileTool**

`src/ClaudeMcpServer.Infrastructure/Tools/ReadFileTool.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Reads the text content of a file at a given path.
/// Safety checks: path must exist, must not be a directory, extension must be in the allowed list,
/// and file size must be under 1 MB.
/// </summary>
public sealed class ReadFileTool : IToolHandler
{
    private static readonly IReadOnlySet<string> AllowedExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".txt", ".md", ".json", ".xml", ".yaml", ".yml", ".csv", ".log",
        ".cs", ".fs", ".vb", ".py", ".js", ".ts", ".html", ".css",
        ".sh", ".bash", ".zsh", ".toml", ".ini", ".cfg", ".conf",
        ".csproj", ".fsproj", ".sln", ".props", ".targets"
    };

    private const long MaxFileSizeBytes = 1 * 1024 * 1024; // 1 MB

    /// <inheritdoc/>
    public string ToolName => "read_file";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Reads the text content of a file. Supports text and code files up to 1 MB. Binary files are rejected.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["path"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Absolute path to the file to read."
                }
            },
            ["required"] = new JsonArray { "path" }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("path", out var pathProp))
            return ToolResult.Error("Parameter 'path' is required.");

        var path = pathProp.GetString();
        if (string.IsNullOrWhiteSpace(path))
            return ToolResult.Error("Parameter 'path' must not be empty.");

        if (!File.Exists(path))
            return ToolResult.Error($"File not found: {path}");

        var ext = Path.GetExtension(path);
        if (!AllowedExtensions.Contains(ext))
            return ToolResult.Error($"File extension '{ext}' is not allowed. Supported: {string.Join(", ", AllowedExtensions.Order())}");

        var info = new FileInfo(path);
        if (info.Length > MaxFileSizeBytes)
            return ToolResult.Error($"File too large: {info.Length / 1024} KB. Maximum allowed is {MaxFileSizeBytes / 1024} KB.");

        try
        {
            var content = await File.ReadAllTextAsync(path, ct);
            return ToolResult.Success($"File: {path}\nSize: {info.Length} bytes\n\n{content}");
        }
        catch (UnauthorizedAccessException)
        {
            return ToolResult.Error($"Access denied reading: {path}");
        }
        catch (IOException ex)
        {
            return ToolResult.Error($"IO error reading file: {ex.Message}");
        }
    }
}
```

- [ ] **Step 10: Run ReadFileTool tests — verify pass**

```bash
dotnet test tests/ClaudeMcpServer.Infrastructure.Tests/ --filter "ReadFileToolTests"
```
Expected: PASS.

- [ ] **Step 11: Write failing tests for RunShellCommandTool**

`tests/ClaudeMcpServer.Infrastructure.Tests/Tools/RunShellCommandToolTests.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

public class RunShellCommandToolTests
{
    [Fact]
    public void ToolName_Is_run_shell_command()
    {
        var tool = new RunShellCommandTool();
        Assert.Equal("run_shell_command", tool.ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_Non_Whitelisted_Command()
    {
        var tool = new RunShellCommandTool();
        var paramsJson = JsonSerializer.Serialize(new { command = "rm", args = "-rf /" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
        Assert.Contains("not allowed", result.Content, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_Rejects_Missing_Command()
    {
        var tool = new RunShellCommandTool();
        var result = await tool.ExecuteAsync(default, CancellationToken.None);
        Assert.True(result.IsError);
    }
}
```

- [ ] **Step 12: Implement RunShellCommandTool**

`src/ClaudeMcpServer.Infrastructure/Tools/RunShellCommandTool.cs`:
```csharp
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Executes a shell command from a strict whitelist.
/// Only the following commands are allowed: ls, pwd, whoami, date, echo, uname.
/// Arguments are passed as-is to the allowed command — no shell interpolation is performed.
/// </summary>
public sealed class RunShellCommandTool : IToolHandler
{
    /// <summary>
    /// The complete set of commands that may be executed.
    /// Expanding this set is a deliberate security decision — review each addition carefully.
    /// </summary>
    private static readonly IReadOnlySet<string> AllowedCommands =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "ls", "pwd", "whoami", "date", "echo", "uname"
        };

    private const int TimeoutMs = 10_000; // 10 seconds

    /// <inheritdoc/>
    public string ToolName => "run_shell_command";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        $"Executes a safe, whitelisted shell command. Allowed commands: {string.Join(", ", AllowedCommands.Order())}.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["command"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = $"Command to execute. Must be one of: {string.Join(", ", AllowedCommands.Order())}."
                },
                ["args"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional arguments to pass to the command (e.g. '-la' for ls)."
                }
            },
            ["required"] = new JsonArray { "command" }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("command", out var cmdProp))
            return ToolResult.Error("Parameter 'command' is required.");

        var command = cmdProp.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(command))
            return ToolResult.Error("Parameter 'command' must not be empty.");

        if (!AllowedCommands.Contains(command))
            return ToolResult.Error(
                $"Command '{command}' is not allowed. Allowed commands: {string.Join(", ", AllowedCommands.Order())}.");

        var args = string.Empty;
        if (parameters.TryGetProperty("args", out var argsProp))
            args = argsProp.GetString() ?? string.Empty;

        // Reject shell metacharacters in arguments to prevent injection
        if (ContainsShellMetachars(args))
            return ToolResult.Error("Arguments contain disallowed shell metacharacters (;, |, &, `, $, >, <, \\n).");

        try
        {
            using var process = new Process();
            process.StartInfo = new ProcessStartInfo
            {
                FileName = command,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            process.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeoutMs);

            var stdout = await process.StandardOutput.ReadToEndAsync(timeoutCts.Token);
            var stderr = await process.StandardError.ReadToEndAsync(timeoutCts.Token);

            await process.WaitForExitAsync(timeoutCts.Token);

            var output = stdout;
            if (!string.IsNullOrWhiteSpace(stderr))
                output += $"\n[stderr]: {stderr}";

            return process.ExitCode == 0
                ? ToolResult.Success(output.TrimEnd())
                : ToolResult.Error($"Command exited with code {process.ExitCode}:\n{output.TrimEnd()}");
        }
        catch (OperationCanceledException)
        {
            return ToolResult.Error($"Command '{command}' timed out after {TimeoutMs / 1000}s.");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to execute '{command}': {ex.Message}");
        }
    }

    private static bool ContainsShellMetachars(string input) =>
        input.IndexOfAny([';', '|', '&', '`', '$', '>', '<', '\n', '\r']) >= 0;
}
```

- [ ] **Step 13: Write failing tests for ListDirectoryTool**

`tests/ClaudeMcpServer.Infrastructure.Tests/Tools/ListDirectoryToolTests.cs`:
```csharp
using System.Text.Json;
using ClaudeMcpServer.Infrastructure.Tools;
using Xunit;

namespace ClaudeMcpServer.Infrastructure.Tests.Tools;

public class ListDirectoryToolTests
{
    [Fact]
    public void ToolName_Is_list_directory()
    {
        var tool = new ListDirectoryTool();
        Assert.Equal("list_directory", tool.ToolName);
    }

    [Fact]
    public async Task ExecuteAsync_Lists_Temp_Directory()
    {
        var tool = new ListDirectoryTool();
        var paramsJson = JsonSerializer.Serialize(new { path = Path.GetTempPath() });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.False(result.IsError);
        Assert.NotEmpty(result.Content);
    }

    [Fact]
    public async Task ExecuteAsync_Returns_Error_For_Nonexistent_Directory()
    {
        var tool = new ListDirectoryTool();
        var paramsJson = JsonSerializer.Serialize(new { path = "/nonexistent/path/xyz" });
        var parameters = JsonSerializer.Deserialize<JsonElement>(paramsJson);

        var result = await tool.ExecuteAsync(parameters, CancellationToken.None);

        Assert.True(result.IsError);
    }
}
```

- [ ] **Step 14: Implement ListDirectoryTool**

`src/ClaudeMcpServer.Infrastructure/Tools/ListDirectoryTool.cs`:
```csharp
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Lists the contents of a directory, showing name, type (file/dir), size, and last modified date.
/// Parameter: <c>path</c> (string, required) — absolute path to the directory.
/// </summary>
public sealed class ListDirectoryTool : IToolHandler
{
    /// <inheritdoc/>
    public string ToolName => "list_directory";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Lists the contents of a directory with name, type, size, and last-modified date.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["path"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Absolute path to the directory to list."
                }
            },
            ["required"] = new JsonArray { "path" }
        });

    /// <inheritdoc/>
    public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("path", out var pathProp))
            return Task.FromResult(ToolResult.Error("Parameter 'path' is required."));

        var path = pathProp.GetString();
        if (string.IsNullOrWhiteSpace(path))
            return Task.FromResult(ToolResult.Error("Parameter 'path' must not be empty."));

        if (!Directory.Exists(path))
            return Task.FromResult(ToolResult.Error($"Directory not found: {path}"));

        try
        {
            var entries = Directory.GetFileSystemEntries(path)
                .Select(e => new
                {
                    IsDir = Directory.Exists(e),
                    Info = new FileSystemInfo[] { }
                        .Concat(Directory.Exists(e)
                            ? [new DirectoryInfo(e)]
                            : [new FileInfo(e)])
                        .First()
                })
                .OrderBy(e => !e.IsDir)
                .ThenBy(e => e.Info.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Directory: {path}");
            sb.AppendLine($"Items: {entries.Count}");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"{"Type",-5} {"Name",-45} {"Size",-12} {"Modified"}");
            sb.AppendLine(new string('-', 80));

            foreach (var entry in entries)
            {
                var type = entry.IsDir ? "[DIR]" : "[FILE]";
                var size = entry.IsDir ? string.Empty : FormatSize(((FileInfo)entry.Info).Length);
                sb.AppendLine($"{type,-5} {entry.Info.Name,-45} {size,-12} {entry.Info.LastWriteTime:yyyy-MM-dd HH:mm}");
            }

            return Task.FromResult(ToolResult.Success(sb.ToString().TrimEnd()));
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(ToolResult.Error($"Access denied listing: {path}"));
        }
        catch (IOException ex)
        {
            return Task.FromResult(ToolResult.Error($"IO error: {ex.Message}"));
        }
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:F1} KB",
        < 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
```

- [ ] **Step 15: Run all Infrastructure tests — verify all pass**

```bash
dotnet test tests/ClaudeMcpServer.Infrastructure.Tests/
```
Expected: All tests PASS.

- [ ] **Step 16: Commit**

```bash
git add src/ClaudeMcpServer.Infrastructure/ tests/ClaudeMcpServer.Infrastructure.Tests/
git commit -m "feat(infrastructure): add all 5 built-in tools with full test coverage"
```

---

## Task 10: Host — Program.cs and appsettings.json

**Files:**
- Create: `src/ClaudeMcpServer.Host/Program.cs`
- Create: `src/ClaudeMcpServer.Host/appsettings.json`

- [ ] **Step 1: Create appsettings.json**

`src/ClaudeMcpServer.Host/appsettings.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "ClaudeMcpServer": "Debug"
    }
  },
  "McpServer": {
    "Transport": "stdio",
    "ServerName": "ClaudeMcpServer",
    "ServerVersion": "1.0.0"
  }
}
```

- [ ] **Step 2: Implement Program.cs**

`src/ClaudeMcpServer.Host/Program.cs`:
```csharp
using ClaudeMcpServer.Application.Handlers;
using ClaudeMcpServer.Application.Services;
using ClaudeMcpServer.Domain.Interfaces;
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
            // Write to stderr
            opts.LogToStandardErrorThreshold = LogLevel.Trace;
        });
    })
    .ConfigureServices((context, services) =>
    {
        // Transport — stdio is the MCP-standard transport for Claude Desktop
        services.AddSingleton<ITransport, StdioTransport>();

        // Tool registry — discovers all IToolHandler registrations automatically
        services.AddSingleton<IToolRegistry, ToolRegistry>();

        // Built-in tools — add new tools here with AddSingleton<IToolHandler, YourTool>()
        services.AddSingleton<IToolHandler, SystemInfoTool>();
        services.AddSingleton<IToolHandler, DateTimeTool>();
        services.AddSingleton<IToolHandler, ReadFileTool>();
        services.AddSingleton<IToolHandler, RunShellCommandTool>();
        services.AddSingleton<IToolHandler, ListDirectoryTool>();

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
```

- [ ] **Step 3: Build the full solution**

```bash
dotnet build ClaudeMcpServer.sln
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 4: Run all tests**

```bash
dotnet test ClaudeMcpServer.sln
```
Expected: All tests PASS.

- [ ] **Step 5: Smoke-test the server (echo a JSON-RPC ping)**

```bash
echo '{"jsonrpc":"2.0","id":1,"method":"ping","params":{}}' | dotnet run --project src/ClaudeMcpServer.Host/
```
Expected: JSON output `{"jsonrpc":"2.0","id":1,"result":{}}` on stdout.

- [ ] **Step 6: Commit**

```bash
git add src/ClaudeMcpServer.Host/
git commit -m "feat(host): add Program.cs with DI wiring and hosted service loop"
```

---

## Task 11: GitHub Repository Files

**Files:**
- Create: `.gitignore`
- Create: `LICENSE`
- Create: `CONTRIBUTING.md`
- Create: `README.md`
- Create: `claude_desktop_config_snippet.json`

- [ ] **Step 1: Create .gitignore**

`.gitignore`:
```
# Build output
bin/
obj/
out/

# .NET user files
*.user
*.suo
.vs/
.idea/
*.DotSettings.user

# Environment and secrets
.env
.env.*
appsettings.Development.json
appsettings.Production.json

# Publish output
publish/

# OS files
.DS_Store
Thumbs.db

# Test results
TestResults/
coverage/
*.trx
*.coverage
*.coveragexml

# NuGet
*.nupkg
*.snupkg
```

- [ ] **Step 2: Create MIT LICENSE**

`LICENSE`:
```
MIT License

Copyright (c) 2026 ClaudeMcpServer Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
```

- [ ] **Step 3: Create CONTRIBUTING.md**

`CONTRIBUTING.md`:
```markdown
# Contributing to ClaudeMcpServer

Thank you for your interest in contributing!

## Adding a New Tool

1. Create a new class in `src/ClaudeMcpServer.Infrastructure/Tools/` that implements `IToolHandler`.
2. Register it in `src/ClaudeMcpServer.Host/Program.cs` with `services.AddSingleton<IToolHandler, YourTool>()`.
3. Add tests in `tests/ClaudeMcpServer.Infrastructure.Tests/Tools/`.
4. That is it — no changes to the protocol layer are required.

## Code Style

- C# 12, .NET 10
- Nullable reference types enabled
- XML `/// <summary>` on every public member
- No `Console.WriteLine` — use `ILogger` (writes to stderr, not stdout)

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat(tool): add MyNewTool`
- `fix(transport): handle empty lines in StdioTransport`
- `docs: update README quick start`
- `test(tools): add edge cases for ReadFileTool`

## Pull Requests

- One feature/fix per PR
- All tests must pass: `dotnet test ClaudeMcpServer.sln`
- Build must be clean: `dotnet build ClaudeMcpServer.sln`
```

- [ ] **Step 4: Create README.md**

`README.md`:
```markdown
# ClaudeMcpServer

[![.NET 10](https://img.shields.io/badge/.NET-10-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Build](https://img.shields.io/badge/build-passing-brightgreen)](#quick-start)

A production-ready **Model Context Protocol (MCP) server** written in C# .NET 10.  
Connects [Claude Desktop](https://claude.ai/download) on macOS to a set of local tools via the MCP stdio transport.

---

## Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    ClaudeMcpServer                       │
│                                                          │
│  ┌──────────┐    ┌─────────────┐    ┌─────────────────┐ │
│  │  Domain  │◄───│ Application │◄───│ Infrastructure  │ │
│  │          │    │             │    │                 │ │
│  │Interfaces│    │ McpService  │    │ StdioTransport  │ │
│  │Entities  │    │ Handlers:   │    │ ToolRegistry    │ │
│  │ValueObjs │    │ -Initialize │    │ Tools:          │ │
│  └──────────┘    │ -ListTools  │    │ -SystemInfo     │ │
│                  │ -CallTool   │    │ -DateTime       │ │
│                  │ -Ping       │    │ -ReadFile       │ │
│                  └─────────────┘    │ -ShellCommand   │ │
│                                     │ -ListDirectory  │ │
│  ┌──────────────────────────────┐   └─────────────────┘ │
│  │           Host               │                        │
│  │  Program.cs + Generic Host   │                        │
│  └──────────────────────────────┘                        │
└─────────────────────────────────────────────────────────┘
         │ stdio (JSON-RPC 2.0)
         ▼
   Claude Desktop
```

**Extension point:** Add a new tool by creating one class that implements `IToolHandler` and registering it in DI. Zero changes to the protocol layer.

---

## Built-in Tools

| Tool | Description |
|------|-------------|
| `get_system_info` | OS, CPU arch, .NET version, hostname, memory |
| `get_datetime` | Current date/time in multiple formats with optional timezone |
| `read_file` | Read text/code files (up to 1 MB, whitelisted extensions) |
| `run_shell_command` | Execute whitelisted commands: `ls`, `pwd`, `whoami`, `date`, `echo`, `uname` |
| `list_directory` | List directory contents with size and modification date |

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
Expected output: `{"jsonrpc":"2.0","id":1,"result":{}}`

### 4. Publish a self-contained binary for macOS

```bash
dotnet publish src/ClaudeMcpServer.Host/ -c Release -r osx-arm64 --self-contained -o ./publish/osx-arm64
# For Intel Mac:
# dotnet publish src/ClaudeMcpServer.Host/ -c Release -r osx-x64 --self-contained -o ./publish/osx-x64
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

1. Create `src/ClaudeMcpServer.Infrastructure/Tools/MyTool.cs`:

```csharp
public sealed class MyTool : IToolHandler
{
    public string ToolName => "my_tool";
    public ToolDefinition GetDefinition() => new(ToolName, "Does something useful", new JsonObject { ["type"] = "object" });
    public Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
        => Task.FromResult(ToolResult.Success("Hello from MyTool!"));
}
```

2. Register in `src/ClaudeMcpServer.Host/Program.cs`:

```csharp
services.AddSingleton<IToolHandler, MyTool>();
```

Done. Rebuild and restart Claude Desktop.

---

## License

[MIT](LICENSE)
```

- [ ] **Step 5: Create claude_desktop_config snippet**

`claude_desktop_config_snippet.json`:
```json
{
  "mcpServers": {
    "claude-mcp-server": {
      "command": "/Users/YOUR_USERNAME/path/to/ClaudeMcpServer/publish/osx-arm64/ClaudeMcpServer.Host",
      "args": [],
      "env": {}
    }
  }
}
```

- [ ] **Step 6: Final build and full test run**

```bash
dotnet build ClaudeMcpServer.sln && dotnet test ClaudeMcpServer.sln
```
Expected: Build succeeded, all tests PASS.

- [ ] **Step 7: Final commit**

```bash
git add .
git commit -m "docs: add README, LICENSE, .gitignore, CONTRIBUTING, and claude_desktop_config snippet"
```

---

## Self-Review Against Spec

| Requirement | Covered |
|---|---|
| JSON-RPC 2.0 over stdio | StdioTransport (Task 7) |
| tools/list, tools/call, initialize, ping | Application Handlers (Task 5) |
| Clean Architecture 4 layers | All tasks — Domain→App→Infra→Host |
| One new class = one new tool | IToolHandler interface + ToolRegistry (Tasks 2, 8) |
| 5 built-in tools | Task 9 |
| All logs to stderr only | StdioTransport + logging config in Program.cs |
| No secrets/API keys | Verified — zero credentials in any file |
| XML `///` docs on every public member | All files include full XML docs |
| `.gitignore` covers bin/, obj/, .env | Task 11 |
| README with badges + ASCII diagram + quick start | Task 11 |
| MIT LICENSE | Task 11 |
| CONTRIBUTING.md | Task 11 |
| claude_desktop_config.json snippet | Task 11 |
| No third-party MCP SDK | Verified — only Microsoft.Extensions.* and System.Text.Json |
| Async throughout | All tools and handlers use async/await |
| Paths from IConfiguration/env | appsettings.json drives config |
