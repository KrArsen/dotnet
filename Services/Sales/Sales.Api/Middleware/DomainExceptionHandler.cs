using System.Diagnostics;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Sales.Domain.Exceptions;

namespace Sales.Api.Middleware;

public class DomainExceptionHandler : IExceptionHandler
{
    private readonly ILogger<DomainExceptionHandler> _logger;

    public DomainExceptionHandler(ILogger<DomainExceptionHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Items[CorrelationIdMiddleware.CorrelationIdHeader]?.ToString()
            ?? httpContext.TraceIdentifier;

        var traceId = Activity.Current?.Id ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, errors) = exception switch
        {
            ValidationException vex => (
                StatusCodes.Status400BadRequest,
                "Помилка валідації запиту",
                vex.Message,
                vex.Errors as IDictionary<string, string[]>),

            ArgumentException aex => (
                StatusCodes.Status400BadRequest,
                "Некоректний запит",
                aex.Message,
                null),

            NotFoundException nfe => (
                StatusCodes.Status404NotFound,
                "Ресурс не знайдено",
                nfe.Message,
                null),

            BusinessConflictException bce => (
                StatusCodes.Status409Conflict,
                "Конфлікт бізнес-правил",
                bce.Message,
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Внутрішня помилка сервера",
                "Сталася неочікувана помилка сервера. Спробуйте пізніше або зверніться до адміністратора.",
                null)
        };

        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred processing request {Path} [CorrelationId: {CorrelationId}]",
                httpContext.Request.Path,
                correlationId);
        }
        else
        {
            _logger.LogWarning(
                "Handled domain exception {ExceptionType} processing request {Path} [Status: {StatusCode}, CorrelationId: {CorrelationId}]: {Message}",
                exception.GetType().Name,
                httpContext.Request.Path,
                statusCode,
                correlationId,
                exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        if (errors != null)
        {
            var validationProblem = new HttpValidationProblemDetails(errors)
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
            validationProblem.Extensions["traceId"] = traceId;
            validationProblem.Extensions["correlationId"] = correlationId;

            await httpContext.Response.WriteAsJsonAsync(
                validationProblem,
                options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        }
        else
        {
            var problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
            problemDetails.Extensions["traceId"] = traceId;
            problemDetails.Extensions["correlationId"] = correlationId;

            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                options: (System.Text.Json.JsonSerializerOptions?)null,
                contentType: "application/problem+json",
                cancellationToken: cancellationToken);
        }

        return true;
    }
}
