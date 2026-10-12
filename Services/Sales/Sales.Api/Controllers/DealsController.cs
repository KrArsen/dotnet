using Microsoft.AspNetCore.Mvc;
using Sales.Bll.Caching;
using Sales.Bll.Dto;
using Sales.Bll.Services;
using Sales.Domain.Exceptions;

namespace Sales.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class DealsController : ControllerBase
{
    private readonly IDealService _dealService;
    private readonly IIdempotencyStore _idempotencyStore;

    public DealsController(IDealService dealService, IIdempotencyStore idempotencyStore)
    {
        _dealService = dealService ?? throw new ArgumentNullException(nameof(dealService));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
    }

    /// <summary>
    /// Отримати список угод із фільтрацією та пагінацією через представлення vw_DealSummary
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DealSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DealSummaryDto>>> GetPage(
        [FromQuery] DealQueryDto query,
        CancellationToken ct)
    {
        var result = await _dealService.GetPageAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Отримати детальну інформацію про угоду (включно з позиціями, деталями та історією стадій)
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealDto>> GetById(long id, CancellationToken ct)
    {
        var deal = await _dealService.GetByIdAsync(id, ct);
        if (deal == null)
        {
            throw new NotFoundException($"Угоду з ID {id} не знайдено.");
        }

        return Ok(deal);
    }

    /// <summary>
    /// Отримати агреговану статистику воронки угод за стадіями
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(IReadOnlyList<DealStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DealStatsDto>>> GetStats(CancellationToken ct)
    {
        var stats = await _dealService.GetStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>
    /// Створити нову угоду з позиціями та початковою стадією Lead (з підтримкою Idempotency-Key)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DealDto>> Create(
        [FromBody] CreateDealDto dto,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var username = user ?? "system";

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cached = await _idempotencyStore.GetAsync(idempotencyKey, ct);
            if (cached != null)
            {
                Response.Headers["X-Cache-Lookup"] = "HIT";
                return StatusCode(cached.StatusCode, cached.Body);
            }
        }

        var created = await _dealService.CreateAsync(dto, username, ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _idempotencyStore.SetAsync(idempotencyKey, StatusCodes.Status201Created, created, ct: ct);
        }

        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Додати товарну або послугову позицію до відкритої угоди
    /// </summary>
    [HttpPost("{id:long}/items")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AddItem(
        long id,
        [FromBody] AddDealItemDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        await _dealService.AddItemAsync(id, dto, user ?? "system", ct);
        var updated = await _dealService.GetByIdAsync(id, ct);
        return Ok(updated);
    }

    /// <summary>
    /// Перевести угоду на наступну стадію воронки (Prospect -> Negotiation -> Won)
    /// </summary>
    [HttpPost("{id:long}/advance")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Advance(
        long id,
        [FromBody] AdvanceDealDto? dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        await _dealService.AdvanceAsync(id, user ?? "system", dto?.Reason, ct);
        return NoContent();
    }

    /// <summary>
    /// Успішно закрити угоду (Won).
    /// Демонструє розподілену атомарну транзакцію Unit of Work через два репозиторії:
    /// Dapper Stored Procedure для угоди + ADO.NET для оновлення статистики клієнта.
    /// </summary>
    [HttpPost("{id:long}/win")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Win(
        long id,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cached = await _idempotencyStore.GetAsync(idempotencyKey, ct);
            if (cached != null)
            {
                Response.Headers["X-Cache-Lookup"] = "HIT";
                return StatusCode(cached.StatusCode, cached.Body);
            }
        }

        await _dealService.WinDealAsync(id, user ?? "system", ct);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            await _idempotencyStore.SetAsync(idempotencyKey, StatusCodes.Status204NoContent, null, ct: ct);
        }

        return NoContent();
    }

    /// <summary>
    /// Закрити угоду як програну (Lost) із зазначенням обов'язкової причини зриву
    /// </summary>
    [HttpPost("{id:long}/lose")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Lose(
        long id,
        [FromBody] LoseDealDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        await _dealService.LoseAsync(id, dto, user ?? "system", ct);
        return NoContent();
    }

    /// <summary>
    /// М'яке видалення угоди (Soft Delete)
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        long id,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        await _dealService.SoftDeleteAsync(id, user ?? "system", ct);
        return NoContent();
    }
}
