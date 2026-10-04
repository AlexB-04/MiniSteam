using Microsoft.AspNetCore.Mvc;

namespace MiniSteam.Middleware
{
    public class ApiStatusCodeMiddleware
    {
        private readonly RequestDelegate _next;

        public ApiStatusCodeMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            await _next(context);

            if (!context.Request.Path.StartsWithSegments("/api"))
            {
                return;
            }

            if (context.Response.HasStarted ||
                context.Response.StatusCode < 400 ||
                context.Response.StatusCode >= 600 ||
                !string.IsNullOrWhiteSpace(context.Response.ContentType) ||
                context.Response.ContentLength.HasValue)
            {
                return;
            }

            var title = context.Response.StatusCode switch
            {
                StatusCodes.Status400BadRequest => "Bad request.",
                StatusCodes.Status401Unauthorized => "Authentication required.",
                StatusCodes.Status403Forbidden => "Access denied.",
                StatusCodes.Status404NotFound => "Resource not found.",
                StatusCodes.Status405MethodNotAllowed => "Method not allowed.",
                StatusCodes.Status409Conflict => "Request conflict.",
                StatusCodes.Status429TooManyRequests => "Too many requests.",
                _ => "Request failed."
            };

            context.Response.ContentType = "application/problem+json";

            var problem = new ProblemDetails
            {
                Status = context.Response.StatusCode,
                Title = title,
                Instance = context.Request.Path
            };

            problem.Extensions["traceId"] = context.TraceIdentifier;

            await context.Response.WriteAsJsonAsync(problem);
        }
    }
}
