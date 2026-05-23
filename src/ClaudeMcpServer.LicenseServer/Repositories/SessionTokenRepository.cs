using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

public sealed class SessionTokenRepository(LicenseDbContext db) : ISessionTokenRepository
{
    public async Task AddAsync(SessionToken token, CancellationToken ct = default) =>
        await db.SessionTokens.AddAsync(token, ct);

    public async Task RemoveExpiredForClientAsync(string clientName, CancellationToken ct = default)
    {
        var expired = await db.SessionTokens
            .Where(t => t.ClientName == clientName && t.ExpiresAt < DateTime.UtcNow)
            .ToListAsync(ct);
        db.SessionTokens.RemoveRange(expired);
    }

    public Task SaveChangesAsync(CancellationToken ct = default) =>
        db.SaveChangesAsync(ct);
}
