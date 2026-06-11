using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

namespace ClaudeMcpServer.LicenseServer.Data;

/// <summary>EF Core context for the license database.</summary>
/// <param name="options">Context options supplied by dependency injection.</param>
public sealed class LicenseDbContext(DbContextOptions<LicenseDbContext> options) : DbContext(options)
{
    /// <summary>License keys issued to clients.</summary>
    public DbSet<LicenseKey> LicenseKeys => Set<LicenseKey>();

    /// <summary>Short-lived session tokens.</summary>
    public DbSet<SessionToken> SessionTokens => Set<SessionToken>();

    /// <summary>Administrative access keys.</summary>
    public DbSet<AdminKey> AdminKeys => Set<AdminKey>();

    /// <summary>License plans with their limits and pricing.</summary>
    public DbSet<Plan> Plans => Set<Plan>();

    /// <inheritdoc />
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

        modelBuilder.Entity<Plan>(e =>
        {
            e.HasIndex(p => p.Name).IsUnique();
            e.Property(p => p.Name).HasMaxLength(100);
            e.Property(p => p.Price).HasPrecision(10, 2);
            // Keys outlive their plan: deleting a plan is blocked while keys reference it.
            e.HasMany<LicenseKey>().WithOne(k => k.Plan).HasForeignKey(k => k.PlanId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
