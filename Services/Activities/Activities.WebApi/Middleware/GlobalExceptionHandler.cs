using System.Diagnostics;
using Activities.Domain.Exceptions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace Activities.WebApi.Middleware;

/// <summary>
/// Глобальний обробник винятків у форматі RFC 7807/9457 ProblemDetails
/// </summary>
public class GlobalExceptionHandler : IExceptionHandler
{
    private readonly IHostEnvironment _env;
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(IHostEnvironment env, ILogger<GlobalExceptionHandler> logger)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var correlationId = httpContext.Response.Headers["X-Correlation-Id"].FirstOrDefault()
            ?? httpContext.TraceIdentifier;

        var (statusCode, title, detail, errors) = MapException(exception);

        // Безпечний detail у середовищі Production
        if (!_env.IsDevelopment() && statusCode == StatusCodes.Status500InternalServerError)
        {
            detail = "Виникла непередбачена внутрішня помилка сервера.";
        }

        // Логування винятку виконується рівно один раз
        if (statusCode >= 500)
        {
            _logger.LogError(
                exception,
                "HTTP {StatusCode} | {Title} | CorrelationId: {CorrelationId} | Path: {Path}",
                statusCode,
                title,
                correlationId,
                httpContext.Request.Path);
        }
        else
        {
            _logger.LogWarning(
                "HTTP {StatusCode} | {Title} | {Detail} | CorrelationId: {CorrelationId} | Path: {Path}",
                statusCode,
                title,
                detail,
                correlationId,
                httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = statusCode;
        httpContext.Response.ContentType = "application/problem+json";

        ProblemDetails problemDetails;
        if (errors != null)
        {
            problemDetails = new ValidationProblemDetails(errors)
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
        }
        else
        {
            problemDetails = new ProblemDetails
            {
                Status = statusCode,
                Title = title,
                Detail = detail,
                Instance = httpContext.Request.Path
            };
        }

        problemDetails.Extensions["traceId"] = Activity.Current?.Id ?? httpContext.TraceIdentifier;
        problemDetails.Extensions["correlationId"] = correlationId;

        await httpContext.Response.WriteAsJsonAsync(problemDetails, cancellationToken);
        return true;
    }

    private (int StatusCode, string Title, string Detail, IDictionary<string, string[]>? Errors) MapException(Exception ex)
    {
        return ex switch
        {
            NotFoundException notFound => (
                StatusCodes.Status404NotFound,
                "Ресурс не знайдено",
                notFound.Message,
                null),

            ConflictException conflict => (
                StatusCodes.Status409Conflict,
                "Конфлікт оптимістичного блокування або унікальності",
                conflict.Message,
                null),

            ValidationException domainVal => (
                StatusCodes.Status400BadRequest,
                "Помилка валідації доменного правила",
                domainVal.Message,
                null),

            DomainException domain => (
                StatusCodes.Status400BadRequest,
                "Порушення доменного правила",
                domain.Message,
                null),

            FluentValidation.ValidationException fv => (
                StatusCodes.Status400BadRequest,
                "Помилка валідації вхідних даних",
                "Одне або декілька полів не пройшли валідацію.",
                fv.Errors
                    .GroupBy(e => e.PropertyName, e => e.ErrorMessage)
                    .ToDictionary(g => g.Key, g => g.ToArray())),

            MongoWriteException mwe when mwe.WriteError?.Category == ServerErrorCategory.DuplicateKey => (
                StatusCodes.Status409Conflict,
                "Порушення унікальності MongoDB",
                "Документ з такими унікальними полями уже існує в базі даних.",
                null),

            MongoWriteException mwe => (
                StatusCodes.Status400BadRequest,
                "Помилка запису MongoDB",
                mwe.Message,
                null),

            MongoConnectionException mce => (
                StatusCodes.Status503ServiceUnavailable,
                "База даних MongoDB тимчасово недоступна",
                "Не вдалося встановити з'єднання з кластером MongoDB.",
                null),

            TimeoutException timeout => (
                StatusCodes.Status503ServiceUnavailable,
                "Таймаут очікування відповіді MongoDB",
                "Перевищено час очікування відповіді від сервера MongoDB (Server Selection Timeout).",
                null),

            _ => (
                StatusCodes.Status500InternalServerError,
                "Внутрішня помилка сервера",
                ex.Message,
                null)
        };
    }
}
