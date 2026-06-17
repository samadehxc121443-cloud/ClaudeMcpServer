using System.Text.Json;
using System.Text.Json.Nodes;
using ClaudeMcpServer.Domain.Entities;
using ClaudeMcpServer.Domain.Interfaces;
using ClaudeMcpServer.Domain.ValueObjects;
using ClaudeMcpServer.Infrastructure.Configuration;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Sends an email from the configured iCloud account via SMTP.
/// Supports plain text, HTML with plain-text fallback (multipart/alternative),
/// multiple To recipients, CC and BCC recipients, and file attachments
/// (multipart/mixed). Enforces iCloud's 500-recipients-per-message cap.
/// </summary>
public sealed class SendEmailTool : IToolHandler
{
    private const string MeteredOperation = "email";

    private readonly EmailSettings _settings;
    private readonly ILicenseService _license;
    private readonly ILogger<SendEmailTool> _logger;

    /// <summary>Initializes a new instance of <see cref="SendEmailTool"/>.</summary>
    public SendEmailTool(IOptions<EmailSettings> settings, ILicenseService license, ILogger<SendEmailTool> logger)
    {
        _settings = settings.Value;
        _license = license;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string ToolName => "send_email";

    /// <inheritdoc/>
    public ToolDefinition GetDefinition() => new(
        ToolName,
        "Sends an email from the configured iCloud account via SMTP. Supports plain text or HTML with professional formatting, multiple recipients, CC, BCC, and file attachments. iCloud allows at most 500 recipients per message across to+cc+bcc. IMPORTANT: use the exact parameter names defined below — do not substitute 'html' for 'html_body' or 'attachment_path' for 'attachments'.",
        new JsonObject
        {
            ["type"] = "object",
            ["properties"] = new JsonObject
            {
                ["to"] = new JsonObject
                {
                    ["type"] = new JsonArray { "string", "array" },
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "Recipient(s). Either a single address string ('user@example.com') or an array for mass sends (['a@x.com','b@y.com']). All To recipients are visible to each other."
                },
                ["subject"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Email subject line."
                },
                ["body"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Plain text body. Always required. Used as fallback for email clients that do not render HTML when html_body is also provided."
                },
                ["html_body"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "EXACT KEY: 'html_body' (not 'html', not 'htmlBody'). Optional full HTML document for professional formatting. When provided alongside 'body', creates a multipart/alternative message. Example value: '<!DOCTYPE html><html>...</html>'."
                },
                ["cc"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "EXACT KEY: 'cc' (not 'cc_list', not 'carbon_copy'). Optional array of CC email addresses, visible to all recipients. One recipient: [\"a@x.com\"]. Multiple: [\"a@x.com\",\"b@y.com\"]. Pass an empty array [] to send no copies."
                },
                ["bcc"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "EXACT KEY: 'bcc' (not 'bcc_list', not 'blind_copy'). Optional array of BCC (blind copy) addresses, hidden from all other recipients. Use for invoices or bulk notices where recipients must not see each other's addresses."
                },
                ["attachments"] = new JsonObject
                {
                    ["type"] = "array",
                    ["items"] = new JsonObject { ["type"] = "string" },
                    ["description"] = "EXACT KEY: 'attachments' (not 'attachment_path', not 'attachment'). Optional array of absolute file paths on the server. On Windows use double backslashes: ['C:\\\\temp\\\\file.xlsx']. On macOS use forward slashes: ['/tmp/file.xlsx']. The file must exist on the server at the given path."
                }
            },
            ["required"] = new JsonArray { "to", "subject", "body" }
        });

    /// <inheritdoc/>
    public async Task<ToolResult> ExecuteAsync(JsonElement parameters, CancellationToken ct)
    {
        if (parameters.ValueKind != JsonValueKind.Object)
            return ToolResult.Error("Parameters object is required.");

        if (!parameters.TryGetProperty("subject", out var subjectProp) ||
            !parameters.TryGetProperty("body", out var bodyProp))
        {
            return ToolResult.Error("Parameters 'to', 'subject', and 'body' are required.");
        }

        if (!EmailRecipients.TryParse(parameters, out var recipients, out var recipientError))
            return ToolResult.Error(recipientError!);

        var subject = subjectProp.GetString() ?? string.Empty;
        var body = bodyProp.GetString() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(body))
            return ToolResult.Error("Parameters 'subject' and 'body' must not be empty.");

        // Quota check BEFORE sending: every recipient counts against the daily plan
        // limit. Blocks here so we never exceed the limit; fails open if tracking is down.
        var quota = await _license.CheckUsageAsync(MeteredOperation, recipients.Total, ct);
        if (!quota.Allowed)
        {
            _logger.LogWarning("Send blocked by daily limit: {Used}/{Limit} used, message needs {Count}.",
                quota.Used, quota.Limit, recipients.Total);
            return ToolResult.Error(
                $"Daily email limit reached: {quota.Used}/{quota.Limit} used today, and this message "
              + $"would add {recipients.Total}. Try again tomorrow or upgrade your plan.");
        }

        if (recipients.Total > 1)
            _logger.LogInformation("Sending to {Count} recipients.", recipients.Total);

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
            message.Subject = subject;

            foreach (var addr in recipients.To)
                message.To.Add(MailboxAddress.Parse(addr));
            foreach (var addr in recipients.Cc)
                message.Cc.Add(MailboxAddress.Parse(addr));
            foreach (var addr in recipients.Bcc)
                message.Bcc.Add(MailboxAddress.Parse(addr));

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

            // Record usage AFTER a successful send so failed sends don't consume quota.
            var usage = await _license.RecordUsageAsync(MeteredOperation, recipients.Total, ct);

            var summary = recipients.Total == 1
                ? $"Email sent to {recipients.To[0]}"
                : $"Email sent to {recipients.Total} recipients ({recipients.To.Count} to, {recipients.Cc.Count} cc, {recipients.Bcc.Count} bcc)";
            if (attachmentPaths.Count > 0)
                summary += $" with {attachmentPaths.Count} attachment(s)";
            if (!string.IsNullOrWhiteSpace(htmlBody))
                summary += " (HTML + plain text)";

            // Warn the client when they cross 90% of their daily limit.
            if (usage is { Tracked: true, PercentUsed: >= 90, Limit: not null })
                summary += $". Heads up: you've used {usage.Used}/{usage.Limit} emails today ({usage.PercentUsed}% of your daily limit)";

            return ToolResult.Success($"{summary}.");
        }
        catch (Exception ex)
        {
            return EmailExceptionHelper.Handle(ex, "Failed to send email");
        }
    }
}
