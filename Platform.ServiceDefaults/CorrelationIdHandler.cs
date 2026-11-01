using Microsoft.AspNetCore.Http;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// DelegatingHandler для автоматичної передачі заголовка X-Correlation-Id
/// у всі вихідні HTTP-запити до інших мікросервісів.
/// </summary>
public class CorrelationIdHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _accessor;

    public CorrelationIdHandler(IHttpContextAccessor accessor)
    {
        _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var correlationId = _accessor.HttpContext?.Request.Headers[CorrelationIdMiddleware.HeaderName].ToString();

        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.Remove(CorrelationIdMiddleware.HeaderName);
            request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
