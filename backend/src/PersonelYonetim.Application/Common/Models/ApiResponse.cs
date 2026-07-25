namespace PersonelYonetim.Application.Common.Models;

/// <summary>
/// Tüm API cevapları bu zarf (envelope) ile döner.
/// Frontend her zaman aynı şekli parse eder.
/// </summary>
public sealed class ApiResponse<T>
{
    public bool Success { get; init; }
    public T? Data { get; init; }
    public ApiError? Error { get; init; }
    public string? TraceId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? traceId = null) => new()
    {
        Success = true,
        Data = data,
        TraceId = traceId
    };

    public static ApiResponse<T> Fail(ApiError error, string? traceId = null) => new()
    {
        Success = false,
        Error = error,
        TraceId = traceId
    };
}

public sealed class ApiResponse
{
    public bool Success { get; init; }
    public ApiError? Error { get; init; }
    public string? TraceId { get; init; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;

    public static ApiResponse Ok(string? traceId = null) => new()
    {
        Success = true,
        TraceId = traceId
    };

    public static ApiResponse Fail(ApiError error, string? traceId = null) => new()
    {
        Success = false,
        Error = error,
        TraceId = traceId
    };
}

public sealed class ApiError
{
    public string Code { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; init; }
}

/// <summary>
/// Sayfalı listeler için standart paket.
/// </summary>
public sealed class PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
    public int TotalCount { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
