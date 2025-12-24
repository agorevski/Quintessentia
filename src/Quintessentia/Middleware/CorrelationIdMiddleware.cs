namespace Quintessentia.Middleware
{
    /// <summary>
    /// Middleware that adds correlation ID support for request tracing across services.
    /// </summary>
    public class CorrelationIdMiddleware
    {
        private const string CorrelationIdHeaderName = "X-Correlation-ID";
        private readonly RequestDelegate _next;
        private readonly ILogger<CorrelationIdMiddleware> _logger;

        public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
        {
            _next = next;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var correlationId = GetOrCreateCorrelationId(context);

            // Store in HttpContext.Items for access throughout the request
            context.Items["CorrelationId"] = correlationId;

            // Add to response headers
            context.Response.OnStarting(() =>
            {
                context.Response.Headers[CorrelationIdHeaderName] = correlationId;
                return Task.CompletedTask;
            });

            // Create a logging scope with the correlation ID
            using (_logger.BeginScope(new Dictionary<string, object>
            {
                ["CorrelationId"] = correlationId
            }))
            {
                await _next(context);
            }
        }

        private static string GetOrCreateCorrelationId(HttpContext context)
        {
            // Try to get from request headers first (for distributed tracing)
            if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var existingCorrelationId) &&
                !string.IsNullOrWhiteSpace(existingCorrelationId))
            {
                return existingCorrelationId.ToString();
            }

            // Generate a new correlation ID
            return Guid.NewGuid().ToString();
        }
    }

    /// <summary>
    /// Extension methods for adding CorrelationIdMiddleware to the pipeline.
    /// </summary>
    public static class CorrelationIdMiddlewareExtensions
    {
        public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<CorrelationIdMiddleware>();
        }
    }
}
