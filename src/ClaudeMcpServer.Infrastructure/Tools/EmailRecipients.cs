using System.Text.Json;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// The recipients of an email, split into To, Cc and Bcc lists.
/// Parsing and limit-checking live here (pure, no I/O) so they can be tested
/// without an SMTP connection.
/// </summary>
/// <param name="To">Primary recipients (visible).</param>
/// <param name="Cc">Carbon-copy recipients (visible).</param>
/// <param name="Bcc">Blind carbon-copy recipients (hidden from all others).</param>
public sealed record EmailRecipients(
    IReadOnlyList<string> To,
    IReadOnlyList<string> Cc,
    IReadOnlyList<string> Bcc)
{
    /// <summary>
    /// iCloud caps a single message at 500 recipients across all fields
    /// (Apple support 102198). Enforced here to fail fast with a clear message
    /// instead of letting the SMTP server reject the send.
    /// </summary>
    public const int MaxRecipientsPerMessage = 500;

    /// <summary>Total recipient count across To, Cc and Bcc.</summary>
    public int Total => To.Count + Cc.Count + Bcc.Count;

    /// <summary>
    /// Parses the "to", "cc" and "bcc" parameters. "to" accepts either a single
    /// string (one recipient) or an array of strings (mass send); "cc" and "bcc"
    /// accept arrays. Returns false with a populated <paramref name="error"/> when
    /// the input is invalid or exceeds the iCloud per-message recipient cap.
    /// </summary>
    /// <param name="parameters">The tool's JSON parameters object.</param>
    /// <param name="recipients">The parsed recipients when successful.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    public static bool TryParse(JsonElement parameters, out EmailRecipients recipients, out string? error)
    {
        recipients = new EmailRecipients([], [], []);
        error = null;

        var to = ParseField(parameters, "to");
        var cc = ParseField(parameters, "cc");
        var bcc = ParseField(parameters, "bcc");

        if (to.Count == 0)
        {
            error = "At least one 'to' recipient is required.";
            return false;
        }

        var parsed = new EmailRecipients(to, cc, bcc);
        if (parsed.Total > MaxRecipientsPerMessage)
        {
            error = $"This message has {parsed.Total} recipients, which exceeds iCloud's limit of "
                  + $"{MaxRecipientsPerMessage} per message. Split it into smaller batches.";
            return false;
        }

        recipients = parsed;
        return true;
    }

    /// <summary>
    /// Reads a recipient field that may be a single string or an array of strings,
    /// trimming blanks. Returns an empty list when the field is absent.
    /// </summary>
    private static List<string> ParseField(JsonElement parameters, string name)
    {
        if (!parameters.TryGetProperty(name, out var prop))
            return [];

        var result = new List<string>();
        switch (prop.ValueKind)
        {
            case JsonValueKind.String:
                var single = prop.GetString();
                if (!string.IsNullOrWhiteSpace(single))
                    result.Add(single.Trim());
                break;

            case JsonValueKind.Array:
                foreach (var entry in prop.EnumerateArray())
                {
                    var addr = entry.GetString();
                    if (!string.IsNullOrWhiteSpace(addr))
                        result.Add(addr.Trim());
                }
                break;
        }
        return result;
    }
}
