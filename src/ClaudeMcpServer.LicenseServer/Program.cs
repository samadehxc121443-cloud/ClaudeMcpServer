using System.Text.Json;
using ClaudeMcpServer.LicenseServer.Data;
using ClaudeMcpServer.LicenseServer.DTOs;
using ClaudeMcpServer.LicenseServer.Filters;
using ClaudeMcpServer.LicenseServer.Repositories;
using ClaudeMcpServer.LicenseServer.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// ── Vault ─────────────────────────────────────────────────────────────────────
// When VAULT_ADDR is set (Docker Compose), secrets come from Vault's KV store
// instead of environment variables. Without it (Railway, local dev) the regular
// configuration sources below keep working unchanged.
var vaultAddr = builder.Configuration["VAULT_ADDR"];
if (!string.IsNullOrWhiteSpace(vaultAddr))
{
    var vaultToken = builder.Configuration["VAULT_TOKEN"]
        ?? throw new InvalidOperationException("VAULT_ADDR is set but VAULT_TOKEN is missing.");

    using var http = new HttpClient();
    http.DefaultRequestHeaders.Add("X-Vault-Token", vaultToken);

    var url = $"{vaultAddr.TrimEnd('/')}/v1/secret/data/license-server";
    using var response = await http.GetAsync(url);
    response.EnsureSuccessStatusCode();

    using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    var data = doc.RootElement.GetProperty("data").GetProperty("data");

    // Only infrastructure secrets live in Vault. Application credentials
    // (admin keys, license keys) are data — they live in the database.
    var secrets = new Dictionary<string, string?>();
    if (data.TryGetProperty("ConnectionString", out var connString))
        secrets["ConnectionStrings:DefaultConnection"] = connString.GetString();

    builder.Configuration.AddInMemoryCollection(secrets);
}

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

// ── Keycloak ──────────────────────────────────────────────────────────────────
// When configured, admin endpoints also accept Keycloak bearer tokens carrying
// the "license-admin" realm role. X-Admin-Key (DB) keeps working for
// machine-to-machine access. Without this config, only X-Admin-Key applies.
var keycloakMetadata = builder.Configuration["Keycloak:MetadataAddress"];
if (!string.IsNullOrWhiteSpace(keycloakMetadata))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.MetadataAddress = keycloakMetadata;
            // Dev only: Keycloak speaks plain HTTP inside the compose network.
            opts.RequireHttpsMetadata = false;
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                // Keycloak's issuer depends on where the token was requested
                // from (host vs container network), so both are accepted.
                ValidIssuers = builder.Configuration.GetSection("Keycloak:ValidIssuers").Get<string[]>(),
                ValidateAudience = false
            };
        });
}

builder.Services.AddScoped<ILicenseKeyRepository, LicenseKeyRepository>();
builder.Services.AddScoped<ISessionTokenRepository, SessionTokenRepository>();
builder.Services.AddScoped<IAdminKeyRepository, AdminKeyRepository>();
builder.Services.AddScoped<IPlanRepository, PlanRepository>();
builder.Services.AddScoped<IUsageRepository, UsageRepository>();
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
// Register the real service under its concrete type so the decorators can resolve it
builder.Services.AddScoped<LicenseManagerService>();

// Decorator stack, built via factory (registering the interface directly against a
// decorator whose ctor asks for ILicenseManagerService would be circular).
// With Redis configured:    Logging → Caching → LicenseManagerService
// Without (Railway, local): Logging → LicenseManagerService
var redisConnection = builder.Configuration["REDIS_CONNECTION"];
if (!string.IsNullOrWhiteSpace(redisConnection))
{
    builder.Services.AddStackExchangeRedisCache(opts => opts.Configuration = redisConnection);
    builder.Services.AddScoped<ILicenseManagerService>(sp =>
        new LoggingLicenseManagerService(
            new CachingLicenseManagerService(
                sp.GetRequiredService<LicenseManagerService>(),
                sp.GetRequiredService<IDistributedCache>(),
                sp.GetRequiredService<ILogger<CachingLicenseManagerService>>()),
            sp.GetRequiredService<ILogger<LoggingLicenseManagerService>>()));
}
else
{
    builder.Services.AddScoped<ILicenseManagerService>(sp =>
        new LoggingLicenseManagerService(
            sp.GetRequiredService<LicenseManagerService>(),
            sp.GetRequiredService<ILogger<LoggingLicenseManagerService>>()));
}

var app = builder.Build();

if (!string.IsNullOrWhiteSpace(keycloakMetadata))
    app.UseAuthentication();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<LicenseDbContext>();
    db.Database.Migrate();

    // Bootstrap: without at least one admin key in the DB, the admin API is
    // unreachable. Generate one on first start and log it once (stderr).
    if (!db.AdminKeys.Any(k => k.IsActive))
    {
        var bootstrap = new ClaudeMcpServer.LicenseServer.Models.AdminKey
        {
            Key = Guid.NewGuid().ToString("N"),
            Name = "bootstrap"
        };
        db.AdminKeys.Add(bootstrap);
        db.SaveChanges();
        app.Logger.LogWarning(
            "No active admin keys found. Bootstrap admin key created: {Key} — store it securely and consider rotating it.",
            bootstrap.Key);
    }
}

// ── Health ───────────────────────────────────────────────────────────────────

app.MapGet("/health", async (ILicenseManagerService svc, IHostEnvironment env) =>
{
    try
    {
        var count = await svc.CountKeysAsync();
        return Results.Ok(new { status = "healthy", environment = env.EnvironmentName, keyCount = count, utc = DateTime.UtcNow });
    }
    catch (Exception ex)
    {
        // Internal details are only safe to expose outside Production.
        var error = app.Environment.IsProduction() ? "Service unavailable." : ex.Message;
        return Results.Json(new { status = "unhealthy", environment = env.EnvironmentName, error, utc = DateTime.UtcNow }, statusCode: 503);
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

// ── Usage (key-authenticated: the MCP server reports and queries here) ───────

app.MapPost("/api/usage/report", async (ReportUsageRequest req, ILicenseManagerService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey) || string.IsNullOrWhiteSpace(req.Operation))
        return Results.BadRequest(new { error = "apiKey and operation are required." });

    try
    {
        var result = await svc.ReportUsageAsync(req);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 401);
    }
});

app.MapPost("/api/usage/query", async (ReportUsageRequest req, ILicenseManagerService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.ApiKey) || string.IsNullOrWhiteSpace(req.Operation))
        return Results.BadRequest(new { error = "apiKey and operation are required." });

    try
    {
        var result = await svc.GetUsageTodayAsync(req.ApiKey, req.Operation);
        return Results.Ok(result);
    }
    catch (UnauthorizedAccessException ex)
    {
        return Results.Json(new { error = ex.Message }, statusCode: 401);
    }
});

// ── Plans (public: the portal pricing page reads this) ───────────────────────

app.MapGet("/api/plans", async (ILicenseManagerService svc) =>
{
    var plans = await svc.GetActivePlansAsync();
    return Results.Ok(plans);
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

    try
    {
        var result = await svc.CreateKeyAsync(req);
        return Results.Created($"/api/admin/keys/{result.Id}", result);
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

admin.MapDelete("/keys/{id:int}", async (int id, ILicenseManagerService svc) =>
{
    var result = await svc.RevokeKeyAsync(id);
    return result is null
        ? Results.NotFound(new { error = $"Key {id} not found." })
        : Results.Ok(result);
});

admin.MapPost("/plans", async (CreatePlanRequest req, ILicenseManagerService svc) =>
{
    if (string.IsNullOrWhiteSpace(req.Name))
        return Results.BadRequest(new { error = "name is required." });
    if (req.Price < 0)
        return Results.BadRequest(new { error = "price cannot be negative." });

    try
    {
        var result = await svc.CreatePlanAsync(req);
        return Results.Created($"/api/admin/plans/{result.Id}", result);
    }
    catch (InvalidOperationException ex)
    {
        return Results.Conflict(new { error = ex.Message });
    }
});

admin.MapDelete("/plans/{id:int}", async (int id, ILicenseManagerService svc) =>
{
    var result = await svc.DeactivatePlanAsync(id);
    return result is null
        ? Results.NotFound(new { error = $"Plan {id} not found." })
        : Results.Ok(result);
});

app.Run();
