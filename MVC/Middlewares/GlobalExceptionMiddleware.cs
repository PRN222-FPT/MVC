using System.Diagnostics;
using System.Net;
using System.Text.Json;

namespace MVC.Middlewares;

/// <summary>
/// Catches all unhandled exceptions thrown by downstream middleware and endpoints.
/// <list type="bullet">
///   <item>Logs the exception with structured context (TraceId, HTTP method, path).</item>
///   <item>In <b>Development</b>: re-throws so the developer exception page shows full details.</item>
///   <item>In <b>Production</b> (JSON request): returns a safe <see cref="ErrorResponse"/> payload.</item>
///   <item>In <b>Production</b> (MVC request): redirects to <c>/Home/Error</c>.</item>
/// </list>
/// </summary>
public sealed class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;
    private readonly IHostEnvironment _env;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public GlobalExceptionMiddleware(
        RequestDelegate next,
        ILogger<GlobalExceptionMiddleware> logger,
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
            var traceId = Activity.Current?.Id ?? context.TraceIdentifier;

            _logger.LogError(
                ex,
                "Unhandled exception | TraceId={TraceId} | {Method} {Path}",
                traceId,
                context.Request.Method,
                context.Request.Path);

            // In Development, re-throw so UseDeveloperExceptionPage shows full stack trace
            if (_env.IsDevelopment())
            {
                throw;
            }

            await HandleExceptionAsync(context, ex, traceId);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception, string traceId)
    {
        if (context.Response.HasStarted)
        {
            _logger.LogWarning(
                "Response already started — cannot write error response. TraceId={TraceId}",
                traceId);
            throw exception;
        }

        var statusCode = MapToStatusCode(exception);

        context.Response.Clear();
        context.Response.StatusCode = statusCode;

        // --- JSON for API / AJAX callers ---
        if (IsJsonRequest(context))
        {
            context.Response.ContentType = "application/json";

            var payload = new ErrorResponse
            {
                StatusCode = statusCode,
                Message = GetUserFriendlyMessage(statusCode),
                TraceId = traceId
            };

            await context.Response.WriteAsync(
                JsonSerializer.Serialize(payload, JsonOptions));
            return;
        }

        // --- Redirect for MVC / browser callers ---
        context.Response.Redirect($"/Home/Error?statusCode={statusCode}");
    }

    // ───────── Helpers ─────────

    private static int MapToStatusCode(Exception exception) => exception switch
    {
        KeyNotFoundException              => StatusCodes.Status404NotFound,
        UnauthorizedAccessException       => StatusCodes.Status403Forbidden,
        ArgumentException                 => StatusCodes.Status400BadRequest,
        InvalidOperationException         => StatusCodes.Status409Conflict,
        NotImplementedException           => StatusCodes.Status501NotImplemented,
        OperationCanceledException        => 499,               // Client closed request
        _ => StatusCodes.Status500InternalServerError
    };

    private static string GetUserFriendlyMessage(int statusCode) => statusCode switch
    {
        400 => "The request was invalid.",
        403 => "You do not have permission to access this resource.",
        404 => "The requested resource was not found.",
        409 => "The request could not be completed due to a conflict.",
        499 => "The request was cancelled.",
        501 => "This feature is not implemented yet.",
        _   => "An unexpected error occurred. Please try again later."
    };

    private static bool IsJsonRequest(HttpContext context)
    {
        var accept = context.Request.Headers.Accept.ToString();
        return accept.Contains("application/json", StringComparison.OrdinalIgnoreCase)
            || string.Equals(context.Request.ContentType, "application/json", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Standard JSON error payload returned by <see cref="GlobalExceptionMiddleware"/>
/// for API / AJAX callers.
/// </summary>
public sealed record ErrorResponse
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = string.Empty;
    public string TraceId { get; init; } = string.Empty;
}

/// <summary>
/// Extension method for clean registration in <c>Program.cs</c>.
/// </summary>
public static class GlobalExceptionMiddlewareExtensions
{
    public static IApplicationBuilder UseGlobalExceptionHandler(this IApplicationBuilder app)
    {
        return app.UseMiddleware<GlobalExceptionMiddleware>();
    }
}
