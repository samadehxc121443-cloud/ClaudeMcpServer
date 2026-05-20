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
                .Select(e =>
                {
                    var isDir = Directory.Exists(e);
                    FileSystemInfo info = isDir ? new DirectoryInfo(e) : new FileInfo(e);
                    return (IsDir: isDir, Info: info);
                })
                .OrderBy(e => !e.IsDir)
                .ThenBy(e => e.Info.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var sb = new StringBuilder();
            sb.AppendLine($"Directory: {path}");
            sb.AppendLine($"Items: {entries.Count}");
            sb.AppendLine(new string('-', 80));
            sb.AppendLine($"{"Type",-6} {"Name",-44} {"Size",-12} {"Modified"}");
            sb.AppendLine(new string('-', 80));

            foreach (var (isDir, info) in entries)
            {
                var type = isDir ? "[DIR]" : "[FILE]";
                var size = isDir ? string.Empty : FormatSize(((FileInfo)info).Length);
                sb.AppendLine($"{type,-6} {info.Name,-44} {size,-12} {info.LastWriteTime:yyyy-MM-dd HH:mm}");
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
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):F1} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):F1} GB"
    };
}
