namespace PersonelYonetim.Api.Middleware;

/// <summary>
/// Kimliği doğrulanmış org/harita/mahalle lookup'ları tarayıcıda tutulmaz (yazımdan sonra bayat veri olmasın).
/// Yalnızca geocode yanıtları kısa private cache alır.
/// </summary>
public sealed class LookupCacheHeadersMiddleware(RequestDelegate next)
{
    public Task Invoke(HttpContext context)
    {
        context.Response.OnStarting(() =>
        {
            if (!HttpMethods.IsGet(context.Request.Method) || context.Response.StatusCode != StatusCodes.Status200OK)
                return Task.CompletedTask;

            if (IsGeocode(context.Request.Path))
                context.Response.Headers.CacheControl = "private, max-age=300";
            else if (IsLookup(context.Request.Path))
                context.Response.Headers.CacheControl = "private, no-store";

            return Task.CompletedTask;
        });

        return next(context);
    }

    private static bool IsGeocode(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.StartsWith("/api/map/geocode", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/map/reverse", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsLookup(PathString path)
    {
        var value = path.Value ?? string.Empty;
        return value.Equals("/api/organization/units/tree", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/organization/units/form-options", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/employees/form-options", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/settlements", StringComparison.OrdinalIgnoreCase)
            || value.Equals("/api/map/settlements/summary", StringComparison.OrdinalIgnoreCase);
    }
}
