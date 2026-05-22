using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
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

var app = builder.Build();

// Apply pending EF Core migrations on startup — creates the DB if it doesn't exist.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.Migrate();
}

// ──────────────────────────────────────────────
//  Health check — used by Railway, uptime monitors, and the MCP host
// ──────────────────────────────────────────────

app.MapGet("/health", async (LicenseDbContext db) =>
{
    try
    {
        var keyCount = await db.LicenseKeys.CountAsync();
        return Results.Ok(new
        {
            status     = "healthy",
            keyCount,
            utc        = DateTime.UtcNow
        });
    }
    catch (Exception ex)
    {
        return Results.Json(new
        {
            status  = "unhealthy",
            error   = ex.Message,
            utc     = DateTime.UtcNow
        }, statusCode: 503);
    }
});

// ──────────────────────────────────────────────
//  Public endpoint — called by the MCP server on every tools/call
// ──────────────────────────────────────────────

app.MapPost("/api/license/validate", async (ValidateRequest req, LicenseDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey))
        return Results.BadRequest(new { valid = false, message = "apiKey is required." });

    var entry = await db.LicenseKeys
        .FirstOrDefaultAsync(k => k.Key == req.ApiKey);

    if (entry is null)
        return Results.Ok(new { valid = false, clientName = (string?)null, message = "License key not found." });

    if (!entry.IsActive)
        return Results.Ok(new { valid = false, clientName = entry.ClientName, message = "License key has been revoked." });

    if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        return Results.Ok(new { valid = false, clientName = entry.ClientName, message = $"License expired on {entry.ExpiresAt.Value:yyyy-MM-dd}." });

    entry.LastValidatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new { valid = true, clientName = entry.ClientName, message = (string?)null });
});

// ──────────────────────────────────────────────
//  Token exchange — MCP clients call this once per hour instead of
//  sending their long-lived API key on every request.
// ──────────────────────────────────────────────

app.MapPost("/api/auth/token", async (ValidateRequest req, LicenseDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey))
        return Results.BadRequest(new { error = "apiKey is required." });

    var entry = await db.LicenseKeys.FirstOrDefaultAsync(k => k.Key == req.ApiKey);

    if (entry is null)
        return Results.Json(new { error = "License key not found." }, statusCode: 401);

    if (!entry.IsActive)
        return Results.Json(new { error = "License key has been revoked." }, statusCode: 401);

    if (entry.ExpiresAt.HasValue && entry.ExpiresAt.Value < DateTime.UtcNow)
        return Results.Json(new { error = $"License expired on {entry.ExpiresAt.Value:yyyy-MM-dd}." }, statusCode: 401);

    // Prune expired tokens for this client to keep the table lean.
    var expired = db.SessionTokens.Where(t => t.ClientName == entry.ClientName && t.ExpiresAt < DateTime.UtcNow);
    db.SessionTokens.RemoveRange(expired);

    var session = new SessionToken
    {
        Token = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"), // 64 hex chars
        ClientName = entry.ClientName,
        IssuedAt = DateTime.UtcNow,
        ExpiresAt = DateTime.UtcNow.AddHours(1)
    };

    db.SessionTokens.Add(session);
    entry.LastValidatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new { token = session.Token, clientName = session.ClientName, expiresAt = session.ExpiresAt });
});

// ──────────────────────────────────────────────
//  Admin endpoints — protected by X-Admin-Key header
// ──────────────────────────────────────────────

var admin = app.MapGroup("/api/admin").AddEndpointFilter(AdminKeyFilter);

admin.MapGet("/keys", async (LicenseDbContext db) =>
{
    var keys = await db.LicenseKeys
        .OrderByDescending(k => k.CreatedAt)
        .Select(k => new
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
        })
        .ToListAsync();

    return Results.Ok(keys);
});

admin.MapPost("/keys", async (CreateKeyRequest req, LicenseDbContext db) =>
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

    db.LicenseKeys.Add(entry);
    await db.SaveChangesAsync();

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

admin.MapDelete("/keys/{id:int}", async (int id, LicenseDbContext db) =>
{
    var entry = await db.LicenseKeys.FindAsync(id);
    if (entry is null) return Results.NotFound(new { error = $"Key {id} not found." });

    entry.IsActive = false;
    await db.SaveChangesAsync();

    return Results.Ok(new { revoked = true, id = entry.Id, clientName = entry.ClientName });
});

app.Run();

// ──────────────────────────────────────────────
//  Admin key middleware filter
// ──────────────────────────────────────────────

static async ValueTask<object?> AdminKeyFilter(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
{
    var config = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
    var expected = config["AdminKey"];

    if (string.IsNullOrWhiteSpace(expected))
        return Results.Problem("AdminKey is not configured on the server.", statusCode: 500);

    // Support both X-Admin-Key and Authorization: Bearer <key>
    string? providedKey = null;
    if (ctx.HttpContext.Request.Headers.TryGetValue("X-Admin-Key", out var xKey))
        providedKey = xKey.ToString().Trim();
    else if (ctx.HttpContext.Request.Headers.TryGetValue("Authorization", out var auth))
        providedKey = auth.ToString().Trim().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

    if (providedKey != expected.Trim())
    {
        return Results.Json(new { error = "Unauthorized." }, statusCode: 401);
    }

    return await next(ctx);
}

// ──────────────────────────────────────────────
//  Request DTOs
// ──────────────────────────────────────────────

record ValidateRequest(string ApiKey);
record CreateKeyRequest(string ClientName, string? Notes, string? PlanName, DateTime? ExpiresAt, int? DurationDays);
