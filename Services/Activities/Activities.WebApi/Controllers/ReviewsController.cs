using System.Diagnostics;
using Activities.Application.Activities.Commands.DeleteActivity;
using Activities.Application.Common.Models;
using Activities.Application.Reviews.Commands.CreateReview;
using Activities.Application.Reviews.Commands.UpdateReview;
using Activities.Application.Reviews.Queries.GetReviewById;
using Activities.Application.Reviews.Queries.GetReviewsList;
using Activities.WebApi.Contracts;
using Activities.WebApi.Metrics;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace Activities.WebApi.Controllers;

[Route("api/reviews")]
public class ReviewsController : ApiControllerBase
{
    private readonly IMapper _mapper;
    private readonly ReviewMetrics? _metrics;

    public ReviewsController(IMapper mapper, ReviewMetrics? metrics = null)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _metrics = metrics;
    }

    /// <summary>
    /// Отримати список відгуків з пагінацією
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ReviewDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ReviewDto>>> GetPaged(
        [FromQuery] long? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var query = new GetReviewsListQuery(productId, page, pageSize);
            var result = await Sender.Send(query, ct);
            _metrics?.RecordDuration("list_reviews", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(result);
        }
        catch
        {
            _metrics?.RecordDuration("list_reviews", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Отримати відгук за ID із заголовком ETag
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ReviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReviewDto>> GetById(string id, CancellationToken ct)
    {
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var query = new GetReviewByIdQuery(id);
            var result = await Sender.Send(query, ct);

            Response.Headers.ETag = $"\"{result.Version}\"";
            _metrics?.RecordDuration("get_review", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(result);
        }
        catch
        {
            _metrics?.RecordDuration("get_review", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Створити новий відгук на товар або послугу
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<string>> Create(
        [FromBody] CreateReviewRequest request,
        CancellationToken ct)
    {
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var command = _mapper.Map<CreateReviewCommand>(request);
            var id = await Sender.Send(command, ct);

            _metrics?.ReviewCreated(request.Rating);
            _metrics?.RecordDuration("create_review", success: true, sw.Elapsed.TotalMilliseconds);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }
        catch
        {
            _metrics?.RecordDuration("create_review", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Оновити відгук (з перевіркою If-Match для оптимістичного блокування)
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateReviewRequest request,
        [FromHeader(Name = "If-Match")] string? ifMatch,
        CancellationToken ct)
    {
        var expectedVersion = request.ExpectedVersion ?? 1;

        if (!string.IsNullOrWhiteSpace(ifMatch))
        {
            var cleanedHeader = ifMatch.Trim().Trim('"');
            if (int.TryParse(cleanedHeader, out var parsedVersion))
            {
                expectedVersion = parsedVersion;
            }
        }

        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var command = new UpdateReviewCommand(id, request.Text, request.Rating, expectedVersion);
            await Sender.Send(command, ct);

            _metrics?.RecordDuration("update_review", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("update_review", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Видалити відгук за ID
    /// </summary>
    [HttpDelete("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
    {
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var command = new DeleteActivityCommand(id);
            await Sender.Send(command, ct);

            _metrics?.RecordDuration("delete_review", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("delete_review", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }
}
