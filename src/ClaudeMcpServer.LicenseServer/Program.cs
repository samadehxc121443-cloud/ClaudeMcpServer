using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Filters;
using ClaudeMcpServer.LicenseServer.Models;
using ClaudeMcpServer.LicenseServer.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LicenseDbContext>(opts =>
{
    var raw =
        builder.Configuration["DATABASE_URL"] ??
        builder.Configuration.GetConnectionString("DefaultConnection") ??
        throw new InvalidOperationException("No database connection string configured.");

    // Railway injects DATABASE_URL as a URI (postgresql://user:pass@host:port/db).
    // Parse it manually so Npgsql's ADO.NET key-value parser never sees the URI directly.
    string connectionString = raw;
    if (raw.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        raw.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        var uri = new Uri(raw);
        var parts = uri.UserInfo.Split(':', 2);
        connectionString = $"Host={uri.Host};Port={uri.Port};Database={uri.AbsolutePath.TrimStart('/')};" +
                           $"Username={Uri.UnescapeDataString(parts[0])};Password={Uri.UnescapeDataString(parts[1])};" +
                           "SSL Mode=Require;Trust Server Certificate=true";
    }

    opts.UseNpgsql(connectionString);
});

builder.Services.AddScoped<ILicenseKeyRepository, LicenseKeyRepository>();
builder.Services.AddScoped<ISessionTokenRepository, SessionTokenRepository>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.Migrate();
}

// ── Health ──────────────────────────────────────────────────────────────────

app.MapGet("/health", async (ILicenseKeyRepository repo) =>
{
    try
    {
        var keys = await repo.GetAllAsync();
        return Results.Ok(new { status = "healthy", keyCount = keys.Count, utc = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message, utc = DateTime.UtcNow }, statusCode: 503);
    }
});

// ── Public ───────────────────────────────────────────────────────────────────

app.MapPost("/api/license/validate", async (ValidateRequest req, ILicenseKeyRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey))
        return Results.BadRequest(new { valid = false, message = "apiKey is required." });

    var entry = await repo.GetByKeyAsync(req.ApiKey);

    if (entry is null)
        return Results.Ok(new { valid = false, clientName = (string?)null, message = "License key not found." });

    if (!entry.IsActive)
        return Results.Ok(new { valid = false, clientName = entry.ClientName, message = "License key has been revoked." });

    if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        return Results.Ok(new { valid = false, clientName = entry.ClientName, message = $"License expired on {entry.ExpiresAt.Value:yyyy-MM-dd}." });

    entry.LastValidatedAt = DateTime.UtcNow;
    await repo.SaveChangesAsync();

    return Results.Ok(new { valid = true, clientName = entry.ClientName, message = (string?)null });
});

// ── Token exchange ────────────────────────────────────────────────────────────

app.MapPost("/api/auth/token", async (ValidateRequest req, ILicenseKeyRepository licenseRepo, ISessionTokenRepository tokenRepo) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey))
        return Results.BadRequest(new { error = "apiKey is required." });

    var entry = await licenseRepo.GetByKeyAsync(req.ApiKey);

    if (entry is null)
        return Results.Json(new { error = "License key not found." }, statusCode: 401);

    if (!entry.IsActive)
        return Results.Json(new { error = "License key has been revoked." }, statusCode: 401);

    if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        return Results.Json(new { error = $"License expired on {entry.ExpiresAt.Value:yyyy-MM-dd}." }, statusCode: 401);

    await tokenRepo.RemoveExpiredForClientAsync(entry.ClientName);

    var session = new SessionToken
    {
        Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"),
        ClientName = entry.ClientName,
        IssuedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(1)
    };

    await tokenRepo.AddAsync(session);

    entry.LastValidatedAt = DateTime.UtcNow;
    await licenseRepo.SaveChangesAsync(); // persists both token + LastValidatedAt (shared DbContext)

    return Results.Ok(new { token = session.Token, clientName = session.ClientName, expiresAt = session.ExpiresAt });
});

// ── Admin ─────────────────────────────────────────────────────────────────────

var admin = app.MapGroup("/api/admin").AddEndpointFilter(AdminKeyEndpointFilter.HandleAsync);

admin.MapGet("/keys", async (ILicenseKeyRepository repo) =>
{
    var keys = await repo.GetAllAsync();
    return Results.Ok(keys.Select(k => new
    {
        k.Id,
        k.Key,
        k.ClientName,
        k.Notes,
        k.PlanName,
        k.IsActive,
        k.CreatedAt,
        k.ExpiresAt,
        k.LastValidatedAt
    }));
});

admin.MapPost("/keys", async (CreateKeyRequest req, ILicenseKeyRepository repo) =>
{
    if (string.IsNullOrWhiteSpace(req.ClientName))
        return Results.BadRequest(new { error = "clientName is required." });

    DateTime? expiresAt = req.ExpiresAt
        ?? (req.DurationDays.HasValue ? DateTime.UtcNow.AddDays(req.DurationDays.Value) : null);

    var entry = new LicenseKey
    {
        Key = Guid.NewGuid().ToString("N"),
        ClientName = req.ClientName.Trim(),
        Notes = req.Notes?.Trim(),
        PlanName = req.PlanName?.Trim(),
        IsActive = true,
        CreatedAt = DateTime.UtcNow,
        ExpiresAt = expiresAt
    };

    await repo.AddAsync(entry);
    await repo.SaveChangesAsync();

    return Results.Created($"/api/admin/keys/{entry.Id}", new
    {
        entry.Id,
        entry.Key,
        entry.ClientName,
        entry.Notes,
        entry.PlanName,
        entry.IsActive,
        entry.CreatedAt,
        entry.ExpiresAt
    });
});

admin.MapDelete("/keys/{id:int}", async (int id, ILicenseKeyRepository repo) =>
{
    var entry = await repo.GetByIdAsync(id);
    if (entry is null) return Results.NotFound(new { error = $"Key {id} not found." });

    entry.IsActive = false;
    await repo.SaveChangesAsync();

    return Results.Ok(new { revoked = true, id = entry.Id, clientName = entry.ClientName });
});

app.Run();
