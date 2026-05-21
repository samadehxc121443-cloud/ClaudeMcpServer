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

/// <summary>
/// Lists and extracts attachments from a specific email.
/// Text-based attachments are returned inline; binary attachments are saved to disk
/// when a <c>save_dir</c> path is provided.
/// </summary>
public sealed class GetEmailAttachmentsTool : IToolHandler
{
    private readonly EmailSettings _settings;

    /// <summary>Initializes a new instance of <see cref="GetEmailAttachmentsTool"/>.</summary>
    public GetEmailAttachmentsTool(IOptions<EmailSettings> settings) => _settings = settings.Value;

    /// <inheritdoc/>
    public string ToolName => "get_email_attachments";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Lists and extracts attachments from an email. Text-based attachments (plain text, CSV, JSON, HTML) are returned inline. Binary attachments (PDF, images, Office docs) are saved to disk when save_dir is provided.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["id"] = new JsonObject
                {
                    ["type"] = "integer",
                    ["description"] = "The unique ID of the email (as returned by list_emails or search_emails)."
                },
                ["save_dir"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional absolute directory path where binary attachments will be saved."
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

        var saveDir = parameters.TryGetProperty("save_dir", out var saveDirProp)
            ? saveDirProp.GetString()
            : null;

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(_settings.ImapHost, _settings.ImapPort, true, ct);
            await client.AuthenticateAsync(_settings.Username, _settings.Password, ct);

            var inbox = client.Inbox ?? throw new InvalidOperationException("INBOX folder unavailable.");
            await inbox.OpenAsync(FolderAccess.ReadOnly, ct);

            var message = await inbox.GetMessageAsync(new UniqueId(uid), ct)
                ?? throw new InvalidOperationException($"Email {uid} not found.");

            await client.DisconnectAsync(true, ct);

            var attachments = message.Attachments.OfType<MimePart>().ToList();
            if (attachments.Count == 0)
                return ToolResult.Success($"No attachments found in email {uid}.");

            var sb = new StringBuilder();
            sb.AppendLine($"Found {attachments.Count} attachment(s) in email {uid}:");
            sb.AppendLine();

            foreach (var part in attachments)
            {
                var fileName = part.FileName ?? part.ContentType.Name ?? "unnamed";
                var mimeType = $"{part.ContentType.MediaType}/{part.ContentType.MediaSubtype}";
                var isText = part.ContentType.MediaType.Equals("text", StringComparison.OrdinalIgnoreCase);

                sb.AppendLine($"── {fileName}  [{mimeType}]");

                if (part.Content is null)
                {
                    sb.AppendLine("(no content)");
                    sb.AppendLine();
                    continue;
                }

                using var ms = new MemoryStream();
                await part.Content.DecodeToAsync(ms, ct);

                if (isText)
                {
                    var text = Encoding.UTF8.GetString(ms.ToArray());
                    sb.AppendLine(text);
                }
                else if (!string.IsNullOrWhiteSpace(saveDir))
                {
                    Directory.CreateDirectory(saveDir);
                    var safeName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
                    var savePath = Path.Combine(saveDir, safeName);
                    await File.WriteAllBytesAsync(savePath, ms.ToArray(), ct);
                    sb.AppendLine($"Saved to: {savePath}  ({ms.Length:N0} bytes)");
                }
                else
                {
                    sb.AppendLine($"Binary attachment — {ms.Length:N0} bytes. Provide save_dir to save to disk.");
                }

                sb.AppendLine();
            }

            return ToolResult.Success(sb.ToString());
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to get attachments for email {uid}: {ex.Message}");
        }
    }
}
