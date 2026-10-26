using Activities.Application.Activities.Commands.DeleteActivity;
using Activities.Application.Common.Models;
using Activities.Application.Reviews.Commands.CreateReview;
using Activities.Application.Reviews.Commands.UpdateReview;
using Activities.Application.Reviews.Queries.GetReviewById;
using Activities.Application.Reviews.Queries.GetReviewsList;
using Activities.WebApi.Contracts;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace Activities.WebApi.Controllers;

[Route("api/reviews")]
public class ReviewsController : ApiControllerBase
{
    private readonly IMapper _mapper;

    public ReviewsController(IMapper mapper)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
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
        var query = new GetReviewsListQuery(productId, page, pageSize);
        var result = await Sender.Send(query, ct);
        return Ok(result);
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
        var query = new GetReviewByIdQuery(id);
        var result = await Sender.Send(query, ct);

        Response.Headers.ETag = $"\"{result.Version}\"";
        return Ok(result);
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
        var command = _mapper.Map<CreateReviewCommand>(request);
        var id = await Sender.Send(command, ct);

        return CreatedAtAction(nameof(GetById), new { id }, id);
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

        var command = new UpdateReviewCommand(id, request.Text, request.Rating, expectedVersion);
        await Sender.Send(command, ct);

        return NoContent();
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
        var command = new DeleteActivityCommand(id);
        await Sender.Send(command, ct);

        return NoContent();
    }
}
