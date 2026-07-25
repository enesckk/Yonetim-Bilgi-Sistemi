using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PersonelYonetim.Application.Common;
using PersonelYonetim.Application.Common.Exceptions;
using PersonelYonetim.Application.Common.Models;

namespace PersonelYonetim.Api.Middleware;

/// <summary>
/// Yakalanmamış / iş kuralı hatalarını standart ApiResponse zarfına çevirir.
/// Stack trace production'da sızdırılmaz.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await WriteErrorAsync(context, ex);
        }
    }

    private async Task WriteErrorAsync(HttpContext context, Exception exception)
    {
        var traceId = context.TraceIdentifier;

        var (status, error) = exception switch
        {
            ValidationException vex => (
                (int)HttpStatusCode.BadRequest,
                new ApiError
                {
                    Code = vex.Code,
                    Message = vex.Message,
                    ValidationErrors = vex.Errors
                }),
            AppException app => (
                app.StatusCode,
                new ApiError { Code = app.Code, Message = app.Message }),
            DbUpdateConcurrencyException => (
                (int)HttpStatusCode.Conflict,
                new ApiError
                {
                    Code = ErrorCodes.Conflict,
                    Message = "Kayıt başka bir işlemle değişmiş veya eklenememiş olabilir. Sayfayı yenileyip tekrar deneyin."
                }),
            _ => (
                (int)HttpStatusCode.InternalServerError,
                new ApiError
                {
                    Code = ErrorCodes.Unexpected,
                    Message = _env.IsDevelopment()
                        ? exception.Message
                        : "Beklenmeyen bir hata oluştu."
                })
        };

        if (status >= 500)
            _logger.LogError(exception, "Unhandled exception. TraceId={TraceId}", traceId);
        else
            _logger.LogWarning(exception, "Handled business exception. TraceId={TraceId} Code={Code}", traceId, error.Code);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = status;

        var payload = ApiResponse.Fail(error, traceId);
        await context.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
