using Microsoft.AspNetCore.Mvc;

namespace MiniSteam.Middleware
{
    public class ApiExceptionMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly ILogger<ApiExceptionMiddleware> _logger;

        public ApiExceptionMiddleware(
            RequestDelegate next,
            ILogger<ApiExceptionMiddleware> logger)
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
            catch (Exception exception) when (
                context.Request.Path.StartsWithSegments("/api") ||
                context.Request.Path.StartsWithSegments("/health"))
            {
                _logger.LogError(
                    exception,
                    "Unhandled API exception for {Method} {Path}. TraceId: {TraceId}",
                    context.Request.Method,
                    context.Request.Path,
                    context.TraceIdentifier);

                if (context.Response.HasStarted)
                {
                    throw;
                }

                context.Response.Clear();
                context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                context.Response.ContentType = "application/problem+json";

                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status500InternalServerError,
                    Title = "An unexpected server error occurred.",
                    Detail = "The request could not be completed. Use the traceId when reporting the problem.",
                    Instance = context.Request.Path
                };

                problem.Extensions["traceId"] = context.TraceIdentifier;

                await context.Response.WriteAsJsonAsync(problem);
            }
        }
    }
}
