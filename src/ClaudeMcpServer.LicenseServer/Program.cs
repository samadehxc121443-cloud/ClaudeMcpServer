using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LicenseDbContext>(opts =>
    opts.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=licenses.db"));

var app = builder.Build();

// Auto-migrate on startup so the DB is always up to date without manual steps.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    var dataSource = db.Database.GetDbConnection().DataSource;
    if (!string.IsNullOrWhiteSpace(dataSource))
        Directory.CreateDirectory(Path.GetDirectoryName(dataSource)!);
    db.Database.EnsureCreated();
}

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

    entry.LastValidatedAt = DateTime.UtcNow;
    await db.SaveChangesAsync();

    return Results.Ok(new { valid = true, clientName = entry.ClientName, message = (string?)null });
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
            k.IsActive,
            k.CreatedAt,
            k.LastValidatedAt
        })
        .ToListAsync();

    return Results.Ok(keys);
});

admin.MapPost("/keys", async (CreateKeyRequest req, LicenseDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(req.ClientName))
        return Results.BadRequest(new { error = "clientName is required." });

    var entry = new LicenseKey
    {
        Key = Guid.NewGuid().ToString("N"),
        ClientName = req.ClientName.Trim(),
        Notes = req.Notes?.Trim(),
        IsActive = true,
        CreatedAt = DateTime.UtcNow
    };

    db.LicenseKeys.Add(entry);
    await db.SaveChangesAsync();

    return Results.Created($"/api/admin/keys/{entry.Id}", new
    {
        entry.Id,
        entry.Key,
        entry.ClientName,
        entry.Notes,
        entry.IsActive,
        entry.CreatedAt
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

    if (!ctx.HttpContext.Request.Headers.TryGetValue("X-Admin-Key", out var provided) ||
        provided != expected)
    {
        return Results.Json(new { error = "Unauthorized." }, statusCode: 401);
    }

    return await next(ctx);
}

// ──────────────────────────────────────────────
//  Request DTOs
// ──────────────────────────────────────────────

record ValidateRequest(string ApiKey);
record CreateKeyRequest(string ClientName, string? Notes);
