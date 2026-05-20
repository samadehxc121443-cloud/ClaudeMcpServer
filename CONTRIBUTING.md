# Contributing to ClaudeMcpServer

Thank you for your interest in contributing!

## Adding a New Tool

1. Create a new class in `src/ClaudeMcpServer.Infrastructure/Tools/` implementing `IToolHandler`.
2. Register it in `src/ClaudeMcpServer.Host/Program.cs` with `services.AddSingleton<IToolHandler, YourTool>()`.
3. Add tests in `tests/ClaudeMcpServer.Infrastructure.Tests/Tools/`.
4. That is it — no changes to the protocol layer are required.

## Code Style

- C# 12, .NET 10
- Nullable reference types enabled (`<Nullable>enable</Nullable>`)
- XML `/// <summary>` on every public member
- No `Console.WriteLine` — use `ILogger` (writes to stderr, keeping stdout clean for JSON-RPC)

## Commit Messages

We use [Conventional Commits](https://www.conventionalcommits.org/):

- `feat(tool): add WeatherTool`
- `fix(transport): handle empty lines in StdioTransport`
- `docs: update README quick start`
- `test(tools): add edge cases for ReadFileTool`

## Pull Requests

- One feature/fix per PR
- All tests must pass: `dotnet test ClaudeMcpServer.sln`
- Build must be clean: `dotnet build ClaudeMcpServer.sln`
