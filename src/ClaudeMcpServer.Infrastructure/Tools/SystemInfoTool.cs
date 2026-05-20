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
