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

/// <summary>Sends an email from the configured iCloud account via SMTP.</summary>
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
        "Sends an email from the configured iCloud account. Supports plain text and optional CC.",
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
                    ["description"] = "Plain text body of the email."
                },
                ["cc"] = new JsonObject
                {
                    ["type"] = "string",
                    ["description"] = "Optional CC email address."
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

        try
        {
            var message = new MimeMessage();
            message.From.Add(new MailboxAddress(_settings.DisplayName, _settings.Username));
            message.To.Add(MailboxAddress.Parse(to));

            if (parameters.TryGetProperty("cc", out var ccProp))
            {
                var cc = ccProp.GetString();
                if (!string.IsNullOrWhiteSpace(cc))
                    message.Cc.Add(MailboxAddress.Parse(cc));
            }

            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(_settings.SmtpHost, _settings.SmtpPort, SecureSocketOptions.StartTls, ct);
            await smtp.AuthenticateAsync(_settings.Username, _settings.Password, ct);
            await smtp.SendAsync(message, ct);
            await smtp.DisconnectAsync(true, ct);

            return ToolResult.Success($"Email sent successfully to {to}.");
        }
        catch (Exception ex)
        {
            return ToolResult.Error($"Failed to send email: {ex.Message}");
        }
    }
}
