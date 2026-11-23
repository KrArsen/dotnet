using System.Diagnostics;
using Activities.Application.Activities.Commands.AddComment;
using Activities.Application.Activities.Commands.CreateActivity;
using Activities.Application.Activities.Commands.DeleteActivity;
using Activities.Application.Activities.Commands.UpdateActivity;
using Activities.Application.Activities.Queries.GetActivitiesList;
using Activities.Application.Activities.Queries.GetActivityById;
using Activities.Application.Common.Models;
using Activities.WebApi.Contracts;
using Activities.WebApi.Metrics;
using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Activities.WebApi.Controllers;

[Route("api/activities")]
[Authorize]
public class ActivitiesController : ApiControllerBase
{
    private readonly IMapper _mapper;
    private readonly ReviewMetrics? _metrics;

    public ActivitiesController(IMapper mapper, ReviewMetrics? metrics = null)
    {
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _metrics = metrics;
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
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var query = new GetActivitiesListQuery(clientId, type, userId, search, productId, page, pageSize);
            var result = await Sender.Send(query, ct);
            _metrics?.RecordDuration("list_activities", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(result);
        }
        catch
        {
            _metrics?.RecordDuration("list_activities", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
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
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var query = new GetActivityByIdQuery(id);
            var result = await Sender.Send(query, ct);

            Response.Headers.ETag = $"\"{result.Version}\"";
            _metrics?.RecordDuration("get_activity", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(result);
        }
        catch
        {
            _metrics?.RecordDuration("get_activity", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
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
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var command = _mapper.Map<CreateActivityCommand>(request);
            var id = await Sender.Send(command, ct);

            _metrics?.ActivityCreated(request.Type);
            _metrics?.RecordDuration("create_activity", success: true, sw.Elapsed.TotalMilliseconds);
            return CreatedAtAction(nameof(GetById), new { id }, id);
        }
        catch
        {
            _metrics?.RecordDuration("create_activity", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
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

        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var command = new UpdateActivityCommand(id, request.Text, request.Rating, request.Outcome, expectedVersion);
            await Sender.Send(command, ct);

            _metrics?.RecordDuration("update_activity", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("update_activity", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
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
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var command = new DeleteActivityCommand(id);
            await Sender.Send(command, ct);

            _metrics?.RecordDuration("delete_activity", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("delete_activity", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
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
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var command = new AddCommentCommand(id, request.Author, request.Text);
            await Sender.Send(command, ct);

            _metrics?.RecordDuration("add_comment", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("add_comment", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }
}
