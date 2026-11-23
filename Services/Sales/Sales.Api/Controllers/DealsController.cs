using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Sales.Api.Metrics;
using Sales.Bll.Caching;
using Sales.Bll.Dto;
using Sales.Bll.Services;
using Sales.Domain.Exceptions;

namespace Sales.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/[controller]")]
[Route("api/orders")]
[Route("orders")]
[Produces("application/json")]
public class DealsController : ControllerBase
{
    private readonly IDealService _dealService;
    private readonly IIdempotencyStore _idempotencyStore;
    private readonly OrderMetrics? _metrics;

    public DealsController(
        IDealService dealService,
        IIdempotencyStore idempotencyStore,
        OrderMetrics? metrics = null)
    {
        _dealService = dealService ?? throw new ArgumentNullException(nameof(dealService));
        _idempotencyStore = idempotencyStore ?? throw new ArgumentNullException(nameof(idempotencyStore));
        _metrics = metrics;
    }

    private string ResolveActor(string? headerUser) =>
        User.FindFirstValue("preferred_username")
        ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? headerUser
        ?? "system";

    /// <summary>
    /// Отримати список угод із фільтрацією та пагінацією через представлення vw_DealSummary
    /// </summary>
    [HttpGet]
    [RequirePermission("orders:read")]
    [ProducesResponseType(typeof(PagedResult<DealSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<DealSummaryDto>>> GetPage(
        [FromQuery] DealQueryDto query,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var result = await _dealService.GetPageAsync(query, ct);
            _metrics?.RecordDuration("list", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(result);
        }
        catch
        {
            _metrics?.RecordDuration("list", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Отримати детальну інформацію про угоду (включно з позиціями, деталями та історією стадій)
    /// </summary>
    [HttpGet("{id:long}")]
    [RequirePermission("orders:read")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DealDto>> GetById(long id, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var deal = await _dealService.GetByIdAsync(id, ct);
            if (deal == null)
            {
                throw new NotFoundException($"Угоду з ID {id} не знайдено.");
            }

            _metrics?.RecordDuration("get_by_id", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(deal);
        }
        catch
        {
            _metrics?.RecordDuration("get_by_id", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Отримати агреговану статистику воронки угод за стадіями
    /// </summary>
    [HttpGet("stats")]
    [RequirePermission("orders:read")]
    [ProducesResponseType(typeof(IReadOnlyList<DealStatsDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<DealStatsDto>>> GetStats(CancellationToken ct)
    {
        var stats = await _dealService.GetStatsAsync(ct);
        return Ok(stats);
    }

    /// <summary>
    /// Створити нову угоду з позиціями та початковою стадією Lead (з підтримкою Idempotency-Key та JWT claims)
    /// </summary>
    [HttpPost]
    [RequirePermission("orders:create")]
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
        var username = ResolveActor(user);

        if (!string.IsNullOrWhiteSpace(idempotencyKey))
        {
            var cached = await _idempotencyStore.GetAsync(idempotencyKey, ct);
            if (cached != null)
            {
                Response.Headers["X-Cache-Lookup"] = "HIT";
                return StatusCode(cached.StatusCode, cached.Body);
            }
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var created = await _dealService.CreateAsync(dto, username, ct);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                await _idempotencyStore.SetAsync(idempotencyKey, StatusCodes.Status201Created, created, ct: ct);
            }

            _metrics?.OrderCreated(created.Stage ?? "Lead");
            _metrics?.RecordDuration("create", success: true, sw.Elapsed.TotalMilliseconds);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch
        {
            _metrics?.RecordDuration("create", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Оновити угоду/замовлення (перевести на наступну стадію або оновити коментар змін від імені JWT-користувача)
    /// </summary>
    [HttpPut("{id:long}")]
    [RequirePermission("orders:update")]
    [ProducesResponseType(typeof(DealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DealDto>> Update(
        long id,
        [FromBody] AdvanceDealDto? dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _dealService.AdvanceAsync(id, ResolveActor(user), dto?.Reason ?? "Updated via PUT /orders/{id}", ct);
            var updated = await _dealService.GetByIdAsync(id, ct);
            if (updated == null)
            {
                throw new NotFoundException($"Угоду з ID {id} не знайдено.");
            }

            _metrics?.RecordDuration("update", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(updated);
        }
        catch
        {
            _metrics?.RecordDuration("update", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Додати товарну або послугову позицію до відкритої угоди
    /// </summary>
    [HttpPost("{id:long}/items")]
    [RequirePermission("orders:update")]
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
        var sw = Stopwatch.StartNew();
        try
        {
            await _dealService.AddItemAsync(id, dto, ResolveActor(user), ct);
            var updated = await _dealService.GetByIdAsync(id, ct);
            _metrics?.RecordDuration("add_item", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(updated);
        }
        catch
        {
            _metrics?.RecordDuration("add_item", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Перевести угоду на наступну стадію воронки (Prospect -> Negotiation -> Won)
    /// </summary>
    [HttpPost("{id:long}/advance")]
    [RequirePermission("orders:update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Advance(
        long id,
        [FromBody] AdvanceDealDto? dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _dealService.AdvanceAsync(id, ResolveActor(user), dto?.Reason, ct);
            _metrics?.RecordDuration("advance", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("advance", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Підтвердити / успішно закрити угоду (Won / Approve — вимагає orders:approve).
    /// Демонструє розподілену атомарну транзакцію Unit of Work через два репозиторії:
    /// Dapper Stored Procedure для угоди + ADO.NET для оновлення статистики клієнта.
    /// </summary>
    [HttpPost("{id:long}/win")]
    [HttpPost("{id:long}/approve")]
    [RequirePermission("orders:approve")]
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

        var sw = Stopwatch.StartNew();
        try
        {
            await _dealService.WinDealAsync(id, ResolveActor(user), ct);

            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                await _idempotencyStore.SetAsync(idempotencyKey, StatusCodes.Status204NoContent, null, ct: ct);
            }

            _metrics?.OrderConfirmed();
            _metrics?.RecordDuration("confirm", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("confirm", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// Закрити угоду як програну (Lost) із зазначенням обов'язкової причини зриву
    /// </summary>
    [HttpPost("{id:long}/lose")]
    [RequirePermission("orders:update")]
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
        var sw = Stopwatch.StartNew();
        try
        {
            await _dealService.LoseAsync(id, dto, ResolveActor(user), ct);
            _metrics?.OrderLost();
            _metrics?.RecordDuration("lose", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("lose", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }

    /// <summary>
    /// М'яке видалення угоди (Soft Delete — вимагає orders:delete, доступно лише admin)
    /// </summary>
    [HttpDelete("{id:long}")]
    [RequirePermission("orders:delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        long id,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await _dealService.SoftDeleteAsync(id, ResolveActor(user), ct);
            _metrics?.RecordDuration("delete", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("delete", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
    }
}
