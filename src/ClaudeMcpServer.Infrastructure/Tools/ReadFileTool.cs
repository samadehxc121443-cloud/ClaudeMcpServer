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
