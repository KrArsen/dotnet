using System.Diagnostics;
using Catalog.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Middleware;

public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;
    private readonly IHostEnvironment _env;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger, IHostEnvironment env)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.CorrelationIdHeader]?.ToString()
            ?? httpContext.TraceIdentifier;

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, type) = exception switch
        {
            EntityNotFoundException enf => (
                StatusCodes.Status404NotFound,
                "Ресурс не знайдено",
                enf.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.5"
            ),
            ValidationException val => (
                StatusCodes.Status400BadRequest,
                "Помилка валідації запиту",
                val.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            ),
            ConflictException conf => (
                StatusCodes.Status409Conflict,
                "Конфлікт бізнес-правил",
                conf.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            ),
            DomainException dom => (
                StatusCodes.Status409Conflict,
                "Порушення доменного правила",
                dom.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            ),
            ArgumentException arg => (
                StatusCodes.Status400BadRequest,
                "Некоректний запит",
                arg.Message,
                "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            ),
            _ => (
                StatusCodes.Status500InternalServerError,
                "Внутрішня помилка сервера",
                _env.IsDevelopment() ? exception.ToString() : "Сталася непередбачена помилка на сервері. Зверніться до адміністратора.",
                "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            )
        };

        // Логування винятку рівно один раз у глобальному обробнику
        if (statusCode >= 500)
        {
            _logger.LogError(exception, "Неперехоплений виняток 500 під час обробки запиту {Path}. CorrelationId: {CorrelationId}",
                httpContext.Request.Path, correlationId);
        }
        else
        {
            _logger.LogWarning("Доменний виняток {StatusCode} ({Title}) під час обробки запиту {Path}: {Message}. CorrelationId: {CorrelationId}",
                statusCode, title, httpContext.Request.Path, exception.Message, correlationId);
        }

        ProblemDetails problemDetails;

        if (exception is ValidationException validationEx && validationEx.Errors.Count > 0)
        {
            var valProblem = new ValidationProblemDetails(validationEx.Errors)
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Type = type
            };
            problemDetails = valProblem;
        }
        else
        {
            problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path,
                Type = type
            };
        }

        problemDetails.Extensions["traceId"] = traceId;
        problemDetails.Extensions["correlationId"] = correlationId;

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }
}
