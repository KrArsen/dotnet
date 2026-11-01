using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Наскрізний Middleware для обробки та генерації X-Correlation-Id
/// (збагачення Serilog LogContext, заголовків відповіді та межове логування запиту).
/// </summary>
public class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";

    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var isGenerated = false;
        if (!context.Request.Headers.TryGetValue(HeaderName, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            value = Guid.NewGuid().ToString();
            context.Request.Headers[HeaderName] = value; // Далі його побачать YARP та HttpClient
            isGenerated = true;
        }

        var correlationId = value.ToString();
        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(HeaderName))
            {
                context.Response.Headers[HeaderName] = correlationId;
            }
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            if (isGenerated)
            {
                _logger.LogDebug(
                    "Generated new CorrelationId {CorrelationId} for incoming request {Method} {Path}",
                    correlationId,
                    context.Request.Method,
                    context.Request.Path);
            }

            _logger.LogInformation(
                "Request started {Method} {Path}",
                context.Request.Method,
                context.Request.Path);

            await _next(context);

            _logger.LogInformation(
                "Request finished with {StatusCode}",
                context.Response.StatusCode);
        }
    }
}
