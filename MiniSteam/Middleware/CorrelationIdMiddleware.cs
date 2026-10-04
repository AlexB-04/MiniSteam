namespace MiniSteam.Middleware
{
    public class CorrelationIdMiddleware
    {
        private const string HeaderName = "X-Correlation-ID";

        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(
            RequestDelegate next,
            ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            string? suppliedCorrelationId = null;

            if (context.Request.Headers.TryGetValue(HeaderName, out var headerValue))
            {
                suppliedCorrelationId = headerValue.ToString().Trim();
            }

            var correlationId = string.IsNullOrWhiteSpace(suppliedCorrelationId)
                ? Guid.NewGuid().ToString("N")
                : suppliedCorrelationId[..Math.Min(suppliedCorrelationId.Length, 100)];

            context.TraceIdentifier = correlationId;
            context.Response.Headers[HeaderName] = correlationId;

            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
            {
                await _next(context);
            }
        }
    }
}
