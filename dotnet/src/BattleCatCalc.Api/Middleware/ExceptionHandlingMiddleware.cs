using System.Text.Json;
using BattleCatCalc.Core.Exceptions;

namespace BattleCatCalc.Api.Middleware;

/// <summary>
/// Global exception handler that maps domain exceptions to RFC 9457 Problem Details responses.
/// </summary>
public sealed class ExceptionHandlingMiddleware
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title) = ex switch
        {
            ParseException => (422, "Parse Error"),
            FetchException => (502, "Upstream Error"),
            SimulationException => (400, "Simulation Error"),
            ArgumentException => (400, "Bad Request"),
            _ => (500, "Internal Server Error")
        };

        // 5xx 錯誤記錄完整堆疊，4xx 僅記錄訊息
        if (statusCode >= 500)
            _logger.LogError(ex, "Unhandled exception on {Method} {Path}", context.Request.Method, context.Request.Path);
        else
            _logger.LogWarning("Domain exception on {Method} {Path}: {Message}", context.Request.Method, context.Request.Path, ex.Message);

        var problem = new
        {
            type = "https://tools.ietf.org/html/rfc9457",
            title,
            status = statusCode,
            // 不洩漏 500 錯誤的內部細節
            detail = statusCode < 500 ? ex.Message : "An unexpected error occurred.",
            instance = context.Request.Path.Value
        };

        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";
        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, JsonOptions));
    }
}
