using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Data;

public sealed class LicenseDbContext(DbContextOptions<LicenseDbContext> options) : DbContext(options)
{
    public DbSet<LicenseKey> LicenseKeys => Set<LicenseKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LicenseKey>(e =>
        {
            e.HasIndex(k => k.Key).IsUnique();
            e.Property(k => k.Key).HasMaxLength(64);
            e.Property(k => k.ClientName).HasMaxLength(200);
        });
    }
}
