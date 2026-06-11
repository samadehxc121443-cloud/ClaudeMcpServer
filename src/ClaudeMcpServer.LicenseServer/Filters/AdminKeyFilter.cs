using ClaudeMcpServer.LicenseServer.Services;

namespace ClaudeMcpServer.LicenseServer.Filters;

public static class AdminKeyEndpointFilter
{
    public static async ValueTask<object?> HandleAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        string? providedKey = null;
        if (ctx.HttpContext.Request.Headers.TryGetValue("X-Admin-Key", out var xKey))
            providedKey = xKey.ToString().Trim();
        else if (ctx.HttpContext.Request.Headers.TryGetValue("Authorization", out var auth))
            providedKey = auth.ToString().Trim().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(providedKey))
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        // Admin keys live in the database, never in configuration.
        var svc = ctx.HttpContext.RequestServices.GetRequiredService<ILicenseManagerService>();
        if (!await svc.IsAdminKeyValidAsync(providedKey, ctx.HttpContext.RequestAborted))
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        return await next(ctx);
    }
}
