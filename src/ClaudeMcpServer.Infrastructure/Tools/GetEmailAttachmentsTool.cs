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
/// Lists and extracts attachments from one or more emails over a single IMAP
/// connection. Text-based attachments are returned inline; binary attachments
/// are saved to disk when a <c>save_dir</c> path is provided.
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
        "Lists and extracts attachments from one or more emails. Text-based attachments (plain text, CSV, JSON, HTML) are returned inline. Binary attachments (PDF, images, Office docs) are saved to disk when save_dir is provided. To extract from many emails at once, pass an array of ids — all are processed over a single IMAP connection (up to 50 per call).",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["id"] = new JsonObject
                {
                    ["type"] = new JsonArray { "integer", "array" },
                    ["items"] = new JsonObject { ["type"] = "integer" },
                    ["description"] = "The email ID(s) from list_emails or search_emails. A single integer (5) for one email, or an array ([5,6,7]) to extract from several at once. Max 50 per call."
                },
                ["save_dir"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional absolute directory path where binary attachments will be saved. Filenames are prefixed with the email id to avoid collisions across emails."
                }
            },
            ["required"] = new JsonArray { "id" }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (!EmailIdSet.TryParse(parameters, out var idSet, out var idError))
            return ToolResult.Error(idError!);

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

            var sb = new StringBuilder();
            var batch = idSet.Ids.Count > 1;
            if (batch)
                sb.AppendLine($"Extracting attachments from {idSet.Ids.Count} emails:").AppendLine();

            // One open connection serves every email — the whole point of the batch.
            foreach (var uid in idSet.Ids)
            {
                var message = await inbox.GetMessageAsync(new UniqueId(uid), ct);
                if (message is null)
                {
                    sb.AppendLine($"# Email {uid}: not found — skipped.").AppendLine();
                    continue;
                }
                await AppendAttachmentsAsync(sb, uid, message, saveDir, batch, ct);
            }

            await client.DisconnectAsync(true, ct);
            return ToolResult.Success(sb.ToString().TrimEnd());
        }
        catch (Exception ex)
        {
            var label = idSet.Ids.Count == 1 ? $"email {idSet.Ids[0]}" : $"{idSet.Ids.Count} emails";
            return EmailExceptionHelper.Handle(ex, $"Failed to get attachments for {label}");
        }
    }

    /// <summary>Appends one email's attachments to the report; binary files are saved when a directory is given.</summary>
    private static async Task AppendAttachmentsAsync(
        StringBuilder sb, uint uid, MimeMessage message, string? saveDir, bool batch, CancellationToken ct)
    {
        var attachments = message.Attachments.OfType<MimePart>().ToList();

        if (attachments.Count == 0)
        {
            sb.AppendLine($"# Email {uid}: no attachments.").AppendLine();
            return;
        }

        sb.AppendLine($"# Email {uid}: {attachments.Count} attachment(s)");

        foreach (var part in attachments)
        {
            var fileName = part.FileName ?? part.ContentType.Name ?? "unnamed";
            var mimeType = $"{part.ContentType.MediaType}/{part.ContentType.MediaSubtype}";
            var isText = part.ContentType.MediaType.Equals("text", StringComparison.OrdinalIgnoreCase);

            sb.AppendLine($"── {fileName}  [{mimeType}]");

            if (part.Content is null)
            {
                sb.AppendLine("(no content)");
                continue;
            }

            using var ms = new MemoryStream();
            await part.Content.DecodeToAsync(ms, ct);

            if (isText)
            {
                sb.AppendLine(Encoding.UTF8.GetString(ms.ToArray()));
            }
            else if (!string.IsNullOrWhiteSpace(saveDir))
            {
                Directory.CreateDirectory(saveDir);
                var safeName = string.Concat(fileName.Split(Path.GetInvalidFileNameChars()));
                // Prefix with the email id so files from different emails never collide.
                var savePath = Path.Combine(saveDir, $"{uid}-{safeName}");
                await File.WriteAllBytesAsync(savePath, ms.ToArray(), ct);
                sb.AppendLine($"Saved to: {savePath}  ({ms.Length:N0} bytes)");
            }
            else
            {
                sb.AppendLine($"Binary attachment — {ms.Length:N0} bytes. Provide save_dir to save to disk.");
            }
        }

        sb.AppendLine();
    }
}
