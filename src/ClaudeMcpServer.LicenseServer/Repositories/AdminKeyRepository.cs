using ClaudeMcpServer.LicenseServer.Data;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

public sealed class AdminKeyRepository(LicenseDbContext db) : IAdminKeyRepository
{
    public Task<bool> ExistsActiveAsync(string key, CancellationToken ct = default) =>
        db.AdminKeys.AnyAsync(k => k.Key == key && k.IsActive, ct);

    public Task<bool> AnyActiveAsync(CancellationToken ct = default) =>
        db.AdminKeys.AnyAsync(k => k.IsActive, ct);
}
