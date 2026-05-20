namespace ClaudeMcpServer.Infrastructure.Configuration;

/// <summary>Holds iCloud IMAP/SMTP connection settings bound from appsettings.json.</summary>
public sealed class EmailSettings
{
    /// <summary>IMAP server hostname.</summary>
    public string ImapHost { get; init; } = "imap.mail.me.com";

    /// <summary>IMAP server port (993 for SSL).</summary>
    public int ImapPort { get; init; } = 993;

    /// <summary>SMTP server hostname.</summary>
    public string SmtpHost { get; init; } = "smtp.mail.me.com";

    /// <summary>SMTP server port (587 for STARTTLS).</summary>
    public int SmtpPort { get; init; } = 587;

    /// <summary>iCloud account username (e.g. user@me.com).</summary>
    public string Username { get; init; } = string.Empty;

    /// <summary>App-specific password generated at appleid.apple.com.</summary>
    public string Password { get; init; } = string.Empty;

    /// <summary>Display name shown in the From field of outgoing emails.</summary>
    public string DisplayName { get; init; } = string.Empty;
}
