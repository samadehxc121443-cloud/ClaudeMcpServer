using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>EF Core implementation of <see cref="ISessionTokenRepository"/>.</summary>
/// <param name="db">The database context.</param>
public sealed class SessionTokenRepository(LicenseDbContext db) : ISessionTokenRepository
{
    /// <inheritdoc />
    public async Task AddAsync(SessionToken token, CancellationToken ct = default) =>
        await db.SessionTokens.AddAsync(token, ct);

    /// <inheritdoc />
    public async Task RemoveExpiredForClientAsync(string clientName, CancellationToken ct = default)
    {
        var expired = await db.SessionTokens
            .Where(t => t.ClientName == clientName && t.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(ct);
        db.SessionTokens.RemoveRange(expired);
    }
}
