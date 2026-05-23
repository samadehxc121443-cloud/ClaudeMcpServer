namespace ClaudeMcpServer.LicenseServer.Filters;

public static class AdminKeyEndpointFilter
{
    public static async ValueTask<object?> HandleAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var config = ctx.HttpContext.RequestServices.GetRequiredService<IConfiguration>();
        var expected = config["AdminKey"];

        if (string.IsNullOrWhiteSpace(expected))
            return Results.Problem("AdminKey is not configured on the server.", statusCode: 500);

        string? providedKey = null;
        if (ctx.HttpContext.Request.Headers.TryGetValue("X-Admin-Key", out var xKey))
            providedKey = xKey.ToString().Trim();
        else if (ctx.HttpContext.Request.Headers.TryGetValue("Authorization", out var auth))
            providedKey = auth.ToString().Trim().Replace("Bearer ", "", StringComparison.OrdinalIgnoreCase);

        if (providedKey != expected.Trim())
            return Results.Json(new { error = "Unauthorized." }, statusCode: 401);

        return await next(ctx);
    }
}
