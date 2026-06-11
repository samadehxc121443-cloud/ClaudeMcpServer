using ClaudeMcpServer.LicenseServer.Data;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Repositories;

/// <summary>EF Core implementation of <see cref="IAdminKeyRepository"/>.</summary>
/// <param name="db">The database context.</param>
public sealed class AdminKeyRepository(LicenseDbContext db) : IAdminKeyRepository
{
    /// <inheritdoc />
    public Task<bool> ExistsActiveAsync(string key, CancellationToken ct = default) =>
        db.AdminKeys.AnyAsync(k => k.Key == key && k.IsActive, ct);

    /// <inheritdoc />
    public Task<bool> AnyActiveAsync(CancellationToken ct = default) =>
        db.AdminKeys.AnyAsync(k => k.IsActive, ct);
}
