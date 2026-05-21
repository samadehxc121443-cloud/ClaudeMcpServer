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

/// <summary>Searches emails in the iCloud inbox by subject, sender, or body text.</summary>
public sealed class SearchEmailsTool : IToolHandler
{
    private readonly EmailSettings _settings;

    /// <summary>Initializes a new instance of <see cref="SearchEmailsTool"/>.</summary>
    public SearchEmailsTool(IOptions<EmailSettings> settings) => _settings = settings.Value;

    /// <inheritdoc/>
    public string ToolName => "search_emails";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Searches the iCloud inbox by subject, sender address, or body text. Returns matching emails with IDs.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["query"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Text to search for."
                },
                ["field"] = new JsonObject
                {
                    ["type"] = "string",
                    ["enum"] = new JsonArray { "subject", "from", "body", "all" },
                    ["description"] = "Field to search in: subject, from, body, or all (default).",
                    ["default"] = "all"
                },
                ["limit"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = "Maximum number of results to return (default 10, max 50).",
                    ["default"] = 10
                }
            },
            ["required"] = new JsonArray { "query" }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("query", out var queryProp))
        {
            return ToolResult.Error("Parameter 'query' (string) is required.");
        }

        var query = queryProp.GetString() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(query))
            return ToolResult.Error("Parameter 'query' must not be empty.");

        var field = "all";
        if (parameters.TryGetProperty("field", out var fieldProp))
            field = fieldProp.GetString() ?? "all";

        int limit = 10;
        if (parameters.TryGetProperty("limit", out var limitProp) && limitProp.TryGetInt32(out var parsedLimit))
            limit = Math.Clamp(parsedLimit, 1, 50);

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, true, ct);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

            var inbox = client.Inbox ?? throw new InvalidOperationException("INBOX folder unavailable.");
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            SearchQuery searchQuery = field switch
            {
                "subject" => SearchQuery.SubjectContains(query),
                "from"    => SearchQuery.FromContains(query),
                "body"    => SearchQuery.BodyContains(query),
                _         => SearchQuery.Or(
                                SearchQuery.SubjectContains(query),
                                SearchQuery.Or(
                                    SearchQuery.FromContains(query),
                                    SearchQuery.BodyContains(query)))
            };

            var uids = (await inbox.SearchAsync(searchQuery, ct)) ?? [];
            var recentUids = uids.TakeLast(limit).ToList();

            if (recentUids.Count == 0)
            {
                await client.DisconnectAsync(true, ct);
                return ToolResult.Success($"No emails found matching '{query}'.");
            }

            var items = (await inbox.FetchAsync(recentUids,
                MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope | MessageSummaryItems.PreviewText, ct)) ?? [];

            var sb = new StringBuilder();
            sb.AppendLine($"Found {uids.Count} match(es) for '{query}' — showing {items.Count}:");
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
            return EmailExceptionHelper.Handle(ex, "Search failed");
        }
    }
}
