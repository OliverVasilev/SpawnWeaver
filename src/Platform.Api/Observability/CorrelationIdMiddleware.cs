namespace Platform.Api.Observability;

/// <summary>
/// Ensures every HTTP request has a correlation id: taken from the <c>X-Correlation-Id</c>
/// header if present, otherwise the request trace identifier. The id is echoed on the
/// response and added to the logging scope so logs are traceable.
/// </summary>
internal sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ILogger<CorrelationIdMiddleware> logger)
    {
        var correlationId = context.Request.Headers.TryGetValue(HeaderName, out var provided)
            && !string.IsNullOrWhiteSpace(provided)
            ? provided.ToString()
            : context.TraceIdentifier;

        context.Response.Headers[HeaderName] = correlationId;

        using (logger.BeginScope(new Dictionary<string, object> { ["CorrelationId"] = correlationId }))
        {
            await _next(context);
        }
    }
}
