using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Activities.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior контролю швидкодії запитів (Warning якщо запит триває довше порогового значення)
/// </summary>
public class PerformanceBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<PerformanceBehavior<TRequest, TResponse>> _logger;
    private readonly long _thresholdMilliseconds;

    public PerformanceBehavior(
        ILogger<PerformanceBehavior<TRequest, TResponse>> logger,
        IConfiguration configuration)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        if (long.TryParse(configuration["Performance:ThresholdMilliseconds"], out var customThreshold))
        {
            _thresholdMilliseconds = customThreshold;
        }
        else
        {
            _thresholdMilliseconds = 500L;
        }
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await next();

        stopwatch.Stop();

        var elapsedMilliseconds = stopwatch.ElapsedMilliseconds;

        if (elapsedMilliseconds > _thresholdMilliseconds)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogWarning(
                "Long Running Request: {RequestName} ({ElapsedMilliseconds} ms) exceeded threshold of {Threshold} ms {@Request}",
                requestName,
                elapsedMilliseconds,
                _thresholdMilliseconds,
                request);
        }

        return response;
    }
}
