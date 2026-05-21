using ClaudeMcpServer.Domain.ValueObjects;
using MailKit.Security;
using System.Net.Sockets;

namespace ClaudeMcpServer.Infrastructure.Tools;

/// <summary>
/// Classifies email connectivity exceptions into user-friendly ToolResult errors.
/// Network and SSL failures return a generic "service unavailable" message;
/// auth failures return a credential hint; unexpected errors surface the raw message.
/// </summary>
internal static class EmailExceptionHelper
{
    internal static ToolResult Handle(Exception ex, string operationName) =>
        ex switch
        {
            OperationCanceledException =>
                ToolResult.Error("Email service timed out. The service may be temporarily unavailable — try again later."),
            SocketException or IOException =>
                ToolResult.Error("Email service is currently unavailable. Please try again later."),
            SslHandshakeException =>
                ToolResult.Error("Email service SSL handshake failed. The service may be temporarily unavailable."),
            AuthenticationException =>
                ToolResult.Error("Email authentication failed. Verify the app-specific password in configuration."),
            _ =>
                ToolResult.Error($"{operationName}: {ex.Message}")
        };
}
