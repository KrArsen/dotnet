using System.Diagnostics;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Activities.Application.Common.Behaviors;

/// <summary>
/// Pipeline Behavior структурованого логування запитів MediatR
/// </summary>
public class LoggingBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger;

    public LoggingBehavior(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;

        _logger.LogInformation("Handling {RequestName} {@Request}", requestName, request);

        var stopwatch = Stopwatch.StartNew();

        // У разі виникнення винятку (наприклад, ValidationException) execution переривається.
        // Виняток не логується тут, щоб уникнути дублювання — він фіксується у GlobalExceptionHandler.
        var response = await next();

        stopwatch.Stop();

        _logger.LogInformation(
            "Handled {RequestName} in {ElapsedMilliseconds} ms with result type {ResponseType}",
            requestName,
            stopwatch.ElapsedMilliseconds,
            typeof(TResponse).Name);

        return response;
    }
}
