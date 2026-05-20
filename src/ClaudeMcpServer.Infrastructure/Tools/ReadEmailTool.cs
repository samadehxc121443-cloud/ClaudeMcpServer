using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using ClaudeMcpServer.Infrastructure.Configuration;
using MailKit;
using MailKit.Net.Imap;
using MimeKit;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>Reads the full content of a specific email by its unique ID.</summary>
public sealed class ReadEmailTool : IToolHandler
{
    private readonly EmailSettings _settings;

    /// <summary>Initializes a new instance of <see cref="ReadEmailTool"/>.</summary>
    public ReadEmailTool(IOptions<EmailSettings> settings) => _settings = settings.Value;

    /// <inheritdoc/>
    public string ToolName => "read_email";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Reads the full content of a specific email. Use the unique ID returned by list_emails or search_emails.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["id"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = "The unique ID of the email to read (as returned by list_emails or search_emails)."
                }
            },
            ["required"] = new JsonArray { "id" }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("id", out var idProp) ||
            !idProp.TryGetUInt32(out var uid))
        {
            return ToolResult.Error("Parameter 'id' (integer) is required.");
        }

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, true, ct);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

            var inbox = client.Inbox ?? throw new InvalidOperationException("INBOX folder unavailable.");
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            var message = await inbox.GetMessageAsync(new MailKit.UniqueId(uid), ct)
                ?? throw new InvalidOperationException($"Email {uid} not found.");

            var sb = new StringBuilder();
            sb.AppendLine($"From   : {message.From}");
            sb.AppendLine($"To     : {message.To}");
            sb.AppendLine($"Date   : {message.Date:yyyy-MM-dd HH:mm:ss zzz}");
            sb.AppendLine($"Subject: {message.Subject}");
            sb.AppendLine(new string('─', 60));
            sb.AppendLine(message.TextBody ?? message.HtmlBody ?? "(no text content)");

            await client.DisconnectAsync(true, ct);
            return ToolResult.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to read email {uid}: {ex.Message}");
        }
    }
}
