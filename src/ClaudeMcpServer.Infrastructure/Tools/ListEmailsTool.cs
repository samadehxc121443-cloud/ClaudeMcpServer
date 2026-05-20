using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using ClaudeMcpServer.Infrastructure.Configuration;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>Lists recent emails from the iCloud inbox.</summary>
public sealed class ListEmailsTool : IToolHandler
{
    private readonly EmailSettings _settings;

    /// <summary>Initializes a new instance of <see cref="ListEmailsTool"/>.</summary>
    public ListEmailsTool(IOptions<EmailSettings> settings) => _settings = settings.Value;

    /// <inheritdoc/>
    public string ToolName => "list_emails";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Lists recent emails from the iCloud inbox. Returns sender, subject, date, and a short preview.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["count"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = "Number of recent emails to retrieve (default 10, max 50).",
                    ["default"] = 10
                }
            }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        int count = 10;
        if (parameters.ValueKind == JsonValueKind.Object &&
            parameters.TryGetProperty("count", out var countProp) &&
            countProp.TryGetInt32(out var parsed))
        {
            count = Math.Clamp(parsed, 1, 50);
        }

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, true, ct);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

            var inbox = client.Inbox ?? throw new InvalidOperationException("INBOX folder unavailable.");
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            int total = inbox.Count;
            int start = Math.Max(0, total - count);

            var uids = (await inbox.SearchAsync(SearchQuery.All, ct)) ?? [];
            var recentUids = uids.Skip(Math.Max(0, uids.Count - count)).ToList();

            var items = (await inbox.FetchAsync(recentUids,
                MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.PreviewText, ct)) ?? [];

            var sb = new StringBuilder();
            sb.AppendLine($"Inbox — {total} total, showing last {items.Count}:");
            sb.AppendLine(new string('─', 60));

            foreach (var item in items.Reverse())
            {
                var from = item.Envelope?.From?.FirstOrDefault()?.ToString() ?? "(unknown)";
                var subject = item.Envelope?.Subject ?? "(no subject)";
                var date = item.Envelope?.Date?.ToString("yyyy-MM-dd HH:mm") ?? "?";
                var preview = item.PreviewText ?? string.Empty;
                if (preview.Length > 80) preview = preview[..80] + "…";

                sb.AppendLine($"[{item.UniqueId}] {date}");
                sb.AppendLine($"  From   : {from}");
                sb.AppendLine($"  Subject: {subject}");
                if (!string.IsNullOrWhiteSpace(preview))
                    sb.AppendLine($"  Preview: {preview}");
                sb.AppendLine();
            }

            await client.DisconnectAsync(true, ct);
            return ToolResult.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to list emails: {ex.Message}");
        }
    }
}
