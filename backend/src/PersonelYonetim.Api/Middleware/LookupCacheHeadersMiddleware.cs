namespace PersonelYonetim.Api.Middleware;

/// <summary>
/// Yalnızca sık değişmeyen lookup GET yanıtlarına kısa private Cache-Control ekler.
/// Personel, mesaj, kimlik ve gösterge paneli yanıtları cache edilmez.
/// </summary>
public sealed class LookupCacheHeadersMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) || context.Response.StatusCode != StatusCodes.Status200OK)
                return Task.CompletedTask;

            if (IsLookup(context.Request.Path))
                context.Response.Headers.CacheControl = "private, max-age=60";

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static bool IsLookup(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Equals("/api/organization/units/tree", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/organization/units/form-options", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/employees/form-options", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/settlements", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/map/settlements/summary", StringComparison.OrdinalIgnoreCase)
            || value.StartsWith("/api/map/geocode", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/map/reverse", StringComparison.OrdinalIgnoreCase);
    }
}
