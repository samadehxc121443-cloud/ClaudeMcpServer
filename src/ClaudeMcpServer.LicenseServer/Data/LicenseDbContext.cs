using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Data;

public sealed class LicenseDbContext(DbContextOptions<LicenseDbContext> options) : DbContext(options)
{
    public DbSet<LicenseKey> LicenseKeys => Set<LicenseKey>();
    public DbSet<SessionToken> SessionTokens => Set<SessionToken>();
    public DbSet<AdminKey> AdminKeys => Set<AdminKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LicenseKey>(e =>
        {
            e.HasIndex(k => k.Key).IsUnique();
            e.Property(k => k.Key).HasMaxLength(64);
            e.Property(k => k.ClientName).HasMaxLength(200);
        });

        modelBuilder.Entity<SessionToken>(e =>
        {
            e.HasIndex(t => t.Token).IsUnique();
            e.Property(t => t.Token).HasMaxLength(128);
            e.Property(t => t.ClientName).HasMaxLength(200);
        });

        modelBuilder.Entity<AdminKey>(e =>
        {
            e.HasIndex(k => k.Key).IsUnique();
            e.Property(k => k.Key).HasMaxLength(64);
            e.Property(k => k.Name).HasMaxLength(200);
        });
    }
}
