using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using ClaudeMcpServer.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Sends an email from the configured iCloud account via SMTP.
/// Supports plain text, HTML with plain-text fallback (multipart/alternative),
/// CC recipients, and file attachments (multipart/mixed).
/// </summary>
public sealed class SendEmailTool : IToolHandler
{
    private readonly EmailSettings _settings;

    /// <summary>Initializes a new instance of <see cref="SendEmailTool"/>.</summary>
    public SendEmailTool(IOptions<EmailSettings> settings) => _settings = settings.Value;

    /// <inheritdoc/>
    public string ToolName => "send_email";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Sends an email from the configured iCloud account. Supports plain text or HTML with professional formatting, optional CC recipients, and file attachments.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["to"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Recipient email address."
                },
                ["subject"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Email subject line."
                },
                ["body"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Plain text body. Used as fallback when html_body is also provided."
                },
                ["html_body"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional HTML body for professional formatting. When provided, creates a multipart/alternative message so clients that don't support HTML receive the plain text fallback."
                },
                ["cc"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional CC email address."
                },
                ["attachments"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Optional list of absolute file paths to attach to the email."
                }
            },
            ["required"] = new JsonArray { "to", "subject", "body" }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
            return ToolResult.Error("Parameters object is required.");

        if (!parameters.TryGetProperty("to", out var toProp) ||
            !parameters.TryGetProperty("subject", out var subjectProp) ||
            !parameters.TryGetProperty("body", out var bodyProp))
        {
            return ToolResult.Error("Parameters 'to', 'subject', and 'body' are required.");
        }

        var to = toProp.GetString() ?? string.Empty;
        var subject = subjectProp.GetString() ?? string.Empty;
        var body = bodyProp.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(to) || string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            return ToolResult.Error("Parameters 'to', 'subject', and 'body' must not be empty.");

        var htmlBody = parameters.TryGetProperty("html_body", out var htmlProp)
            ? htmlProp.GetString()
            : null;

        var attachmentPaths = parameters.TryGetProperty("attachments", out var attProp)
            ? attProp.EnumerateArray().Select(e => e.GetString()).Where(p => !string.IsNullOrWhiteSpace(p)).ToList()
            : [];

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.DisplayName, _settings.Username));
            message.To.Add(MailboxAddress.Parse(to));
            message.Subject = subject;

            if (parameters.TryGetProperty("cc", out var ccProp))
            {
                var cc = ccProp.GetString();
                if (!string.IsNullOrWhiteSpace(cc))
                    message.Cc.Add(MailboxAddress.Parse(cc));
            }

            // Build body: plain text only, or multipart/alternative when HTML is provided
            MimeEntity bodyEntity = string.IsNullOrWhiteSpace(htmlBody)
                ? new TextPart("plain") { Text = body }
                : new MultipartAlternative
                {
                    new TextPart("plain") { Text = body },
                    new TextPart("html")  { Text = htmlBody }
                };

            // Wrap in multipart/mixed when attachments are present
            if (attachmentPaths.Count > 0)
            {
                var mixed = new Multipart("mixed") { bodyEntity };
                foreach (var path in attachmentPaths)
                {
                    if (!File.Exists(path))
                        return ToolResult.Error($"Attachment not found: {path}");

                    var attachment = new MimePart(MimeTypes.GetMimeType(path))
                    {
                        Content = new MimeContent(File.OpenRead(path)),
                        ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                        ContentTransferEncoding = ContentEncoding.Base64,
                        FileName = Path.GetFileName(path)
                    };
                    mixed.Add(attachment);
                }
                message.Body = mixed;
            }
            else
            {
                message.Body = bodyEntity;
            }

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);

            var summary = $"Email sent to {to}";
            if (attachmentPaths.Count > 0)
                summary += $" with {attachmentPaths.Count} attachment(s)";
            if (!string.IsNullOrWhiteSpace(htmlBody))
                summary += " (HTML + plain text)";

            return ToolResult.Success($"{summary}.");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to send email: {ex.Message}");
        }
    }
}
