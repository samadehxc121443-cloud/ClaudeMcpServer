namespace ClaudeMcpServer.Infrastructure.Configuration;

/// <summary>
/// Configuration for remote license validation.
/// Bind from appsettings.json under the "License" section.
/// When <see cref="ServerUrl"/> is empty the server runs in dev mode and skips validation.
/// </summary>
public sealed class LicenseSettings
{
    /// <summary>Gets the URL of the remote license server (e.g. https://licenses.myapp.com).</summary>
    public string ServerUrl { get; init; } = string.Empty;

    /// <summary>Gets the API key that identifies this client installation.</summary>
    public string ApiKey { get; init; } = string.Empty;

    /// <summary>
    /// When true, skips all license validation and runs in development mode.
    /// Must be explicitly set — an empty ServerUrl alone no longer enables dev mode.
    /// Set to false in all production deployments.
    /// </summary>
    public bool DevMode { get; init; } = false;
}
