using System.Text.Json;
using ClaudeMcpServer.LicenseServer.Services;

namespace ClaudeMcpServer.LicenseServer.Filters;

public static class AdminKeyEndpointFilter
{
    public static async ValueTask<object?> HandleAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        // 1) Keycloak bearer token with the "license-admin" realm role (humans, portal).
        if (HasAdminRole(ctx.HttpContext.User))
            return await next(ctx);

        // 2) X-Admin-Key validated against the database (machine-to-machine).
        //    Admin keys live in the database, never in configuration.
        string? providedKey = null;
        if (ctx.HttpContext.Request.Headers.TryGetValue("X-Admin-Key", out var xKey))
            providedKey = xKey.ToString().Trim();

        if (!string.IsNullOrWhiteSpace(providedKey))
        {
            var svc = ctx.HttpContext.RequestServices.GetRequiredService<ILicenseManagerService>();
            if (await svc.IsAdminKeyValidAsync(providedKey, ctx.HttpContext.RequestAborted))
                return await next(ctx);
        }

        return Results.Json(new { error = "Unauthorized." }, statusCode: 401);
    }

    private static bool HasAdminRole(System.Security.Claims.ClaimsPrincipal user)
    {
        if (user.Identity?.IsAuthenticated != true)
            return false;

        // Keycloak puts realm roles in a JSON claim: realm_access = {"roles":[...]}
        var realmAccess = user.FindFirst("realm_access")?.Value;
        if (string.IsNullOrWhiteSpace(realmAccess))
            return false;

        using var doc = JsonDocument.Parse(realmAccess);
        return doc.RootElement.TryGetProperty("roles", out var roles) &&
               roles.EnumerateArray().Any(r => r.GetString() == "license-admin");
    }
}
