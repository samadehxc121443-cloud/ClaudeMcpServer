using System.Text.Json;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// The set of email UIDs to operate on. Parsing and limit-checking live here
/// (pure, no IMAP) so they can be tested without a mail connection.
/// </summary>
/// <param name="Ids">The unique email IDs, in the order given.</param>
public sealed record EmailIdSet(IReadOnlyList<uint> Ids)
{
    /// <summary>
    /// Upper bound on emails processed per call. Each email is fetched over the
    /// same IMAP connection, but a large batch still risks slow runs and iCloud
    /// rate limits, so we cap it and tell the caller to split larger jobs.
    /// </summary>
    public const int MaxEmailsPerCall = 50;

    /// <summary>
    /// Parses the "id" parameter, which accepts either a single integer (one email,
    /// backward compatible) or an array of integers (batch). Returns false with a
    /// populated <paramref name="error"/> when the input is missing, malformed, or
    /// exceeds <see cref="MaxEmailsPerCall"/>.
    /// </summary>
    /// <param name="parameters">The tool's JSON parameters object.</param>
    /// <param name="set">The parsed id set when successful.</param>
    /// <param name="error">A human-readable reason when parsing fails.</param>
    public static bool TryParse(JsonElement parameters, out EmailIdSet set, out string? error)
    {
        set = new EmailIdSet([]);
        error = null;

        if (parameters.ValueKind != JsonValueKind.Object ||
            !parameters.TryGetProperty("id", out var idProp))
        {
            error = "Parameter 'id' is required (an integer, or an array of integers for batch extraction).";
            return false;
        }

        var ids = new List<uint>();
        switch (idProp.ValueKind)
        {
            case JsonValueKind.Number:
                if (!idProp.TryGetUInt32(out var single))
                {
                    error = "Parameter 'id' must be a non-negative integer.";
                    return false;
                }
                ids.Add(single);
                break;

            case JsonValueKind.Array:
                foreach (var entry in idProp.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Number || !entry.TryGetUInt32(out var uid))
                    {
                        error = "Every entry in 'id' must be a non-negative integer.";
                        return false;
                    }
                    ids.Add(uid);
                }
                break;

            default:
                error = "Parameter 'id' must be an integer or an array of integers.";
                return false;
        }

        if (ids.Count == 0)
        {
            error = "At least one email id is required.";
            return false;
        }

        if (ids.Count > MaxEmailsPerCall)
        {
            error = $"Too many emails: {ids.Count} requested, limit is {MaxEmailsPerCall} per call. Split the job into smaller batches.";
            return false;
        }

        set = new EmailIdSet(ids);
        return true;
    }
}
