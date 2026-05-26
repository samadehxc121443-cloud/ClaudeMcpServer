using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Filters;
using ClaudeMcpServer.LicenseServer.Repositories;
using ClaudeMcpServer.LicenseServer.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<LicenseDbContext>(opts =>
{
    var raw =
        builder.Configuration["DATABASE_URL"] ??
        builder.Configuration.GetConnectionString("DefaultConnection") ??
        throw new InvalidOperationException("No database connection string configured.");

    // Railway injects DATABASE_URL as a URI (postgresql://user:pass@host:port/db).
    // Parse manually so Npgsql's ADO.NET key-value parser never sees the URI directly.
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
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<ILicenseManagerService, LicenseManagerService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.Migrate();
}

// ── Health ───────────────────────────────────────────────────────────────────

app.MapGet("/health", async (ILicenseManagerService svc) =>
{
    try
    {
        var count = await svc.CountKeysAsync();
        return Results.Ok(new { status = "healthy", keyCount = count, utc = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        return Results.Json(new { status = "unhealthy", error = ex.Message, utc = DateTime.UtcNow }, statusCode: 503);
    }
});

// ── Public ────────────────────────────────────────────────────────────────────

app.MapPost("/api/license/validate", async (ValidateRequest req, ILicenseManagerService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey))
        return Results.BadRequest(new { valid = false, message = "apiKey is required." });

    var result = await svc.ValidateAsync(req.ApiKey);
    return Results.Ok(result);
});

app.MapPost("/api/auth/token", async (ValidateRequest req, ILicenseManagerService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey))
        return Results.BadRequest(new { error = "apiKey is required." });

    try
    {
        var result = await svc.ExchangeTokenAsync(req.ApiKey);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 401);
    }
});

// ── Admin ─────────────────────────────────────────────────────────────────────

var admin = app.MapGroup("/api/admin").AddEndpointFilter(AdminKeyEndpointFilter.HandleAsync);

admin.MapGet("/keys", async (ILicenseManagerService svc) =>
{
    var keys = await svc.GetAllKeysAsync();
    return Results.Ok(keys);
});

admin.MapPost("/keys", async (CreateKeyRequest req, ILicenseManagerService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.ClientName))
        return Results.BadRequest(new { error = "clientName is required." });

    var result = await svc.CreateKeyAsync(req);
    return Results.Created($"/api/admin/keys/{result.Id}", result);
});

admin.MapDelete("/keys/{id:int}", async (int id, ILicenseManagerService svc) =>
{
    var result = await svc.RevokeKeyAsync(id);
    return result is null
        ? Results.NotFound(new { error = $"Key {id} not found." })
        : Results.Ok(result);
});

app.Run();
