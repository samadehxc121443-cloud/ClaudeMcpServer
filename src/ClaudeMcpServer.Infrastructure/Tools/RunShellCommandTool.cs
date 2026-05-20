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
/// Arguments are passed directly to the allowed command — no shell interpolation is performed.
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

    private const int TimeoutMs = 10_000;

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

        if (ContainsShellMetachars(args))
            return ToolResult.Error("Arguments contain disallowed shell metacharacters (;, |, &, `, $, >, <, newline).");

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
