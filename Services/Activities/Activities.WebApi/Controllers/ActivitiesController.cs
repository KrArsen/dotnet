using Activities.Application.Activities.Commands.AddComment;
using Activities.Application.Activities.Commands.CreateActivity;
using Activities.Application.Activities.Commands.DeleteActivity;
using Activities.Application.Activities.Commands.UpdateActivity;
using Activities.Application.Activities.Queries.GetActivitiesList;
using Activities.Application.Activities.Queries.GetActivityById;
using Activities.Application.Common.Models;
using Activities.WebApi.Contracts;
using AutoMapper;
using Microsoft.AspNetCore.Mvc;

namespace Activities.WebApi.Controllers;

[Route("api/activities")]
public class ActivitiesController : ApiControllerBase
{
    private readonly IMapper _mapper;

    public ActivitiesController(IMapper mapper)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
    }

    /// <summary>
    /// Отримати пагінований список активностей з фільтрацією та кешуванням у пам'яті
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ActivityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ActivityDto>>> GetPaged(
        [FromQuery] long? clientId,
        [FromQuery] string? type,
        [FromQuery] long? userId,
        [FromQuery] string? search,
        [FromQuery] long? productId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var query = new GetActivitiesListQuery(clientId, type, userId, search, productId, page, pageSize);
        var result = await Sender.Send(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Отримати детальну інформацію про активність за її ID (з поверненням ETag версії)
    /// </summary>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(ActivityDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ActivityDto>> GetById(string id, CancellationToken ct)
    {
        var query = new GetActivityByIdQuery(id);
        var result = await Sender.Send(query, ct);

        // Передача актуальної версії документа у HTTP-заголовку ETag
        Response.Headers.ETag = $"\"{result.Version}\"";

        return Ok(result);
    }

    /// <summary>
    /// Створити нову активність або відгук клієнта
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(string), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<string>> Create(
        [FromBody] CreateActivityRequest request,
        CancellationToken ct)
    {
        var command = _mapper.Map<CreateActivityCommand>(request);
        var id = await Sender.Send(command, ct);

        return CreatedAtAction(nameof(GetById), new { id }, id);
    }

    /// <summary>
    /// Оновити текст або рейтинг активності (підтримка оптимістичного блокування через If-Match)
    /// </summary>
    [HttpPut("{id}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        string id,
        [FromBody] UpdateActivityRequest request,
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

        var command = new UpdateActivityCommand(id, request.Text, request.Rating, request.Outcome, expectedVersion);
        await Sender.Send(command, ct);

        return NoContent();
    }

    /// <summary>
    /// Видалити активність за її ID
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

    /// <summary>
    /// Додати коментар команди до активності
    /// </summary>
    [HttpPost("{id}/comments")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddComment(
        string id,
        [FromBody] AddCommentRequest request,
        CancellationToken ct)
    {
        var command = new AddCommentCommand(id, request.Author, request.Text);
        await Sender.Send(command, ct);

        return NoContent();
    }
}
