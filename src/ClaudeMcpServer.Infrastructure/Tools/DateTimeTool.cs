using System.Globalization;
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
        var offset = tz.GetUtcOffset(now);
        var offsetStr = tz == TimeZoneInfo.Utc ? "Z" : $"{offset:+hh\\:mm;-hh\\:mm}";

        var sb = new StringBuilder();
        sb.AppendLine($"Timezone:         {tz.Id} ({tz.DisplayName})");
        sb.AppendLine($"Local time:       {now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"ISO 8601:         {now:yyyy-MM-ddTHH:mm:ss}{offsetStr}");
        sb.AppendLine($"UTC:              {utcNow:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Unix timestamp:   {DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        sb.AppendLine($"Day of week:      {now:dddd}");
        sb.AppendLine($"Week of year:     {CultureInfo.InvariantCulture.Calendar.GetWeekOfYear(now, CalendarWeekRule.FirstFourDayWeek, DayOfWeek.Monday)}");

        return Task.FromResult(ToolResult.Success(sb.ToString().TrimEnd()));
    }
}
