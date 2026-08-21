namespace ConstFire.Backend.Middleware;

public class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var start = DateTime.UtcNow;
        logger.LogInformation("→ {Method} {Path}", context.Request.Method, context.Request.Path);

        await next(context);

        var elapsed = DateTime.UtcNow - start;
        logger.LogInformation("← {Method} {Path} {StatusCode} ({ElapsedMs}ms)",
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            elapsed.TotalMilliseconds);
    }
}
