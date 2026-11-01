using Microsoft.AspNetCore.Mvc;
using Platform.Aggregator.Models;
using Platform.Aggregator.Services;

namespace Platform.Aggregator.Controllers;

[ApiController]
[Route("api/aggregator")]
[Produces("application/json")]
public class AggregatorController : ControllerBase
{
    private readonly IOrderAggregationService _aggregationService;
    private readonly ILogger<AggregatorController> _logger;

    public AggregatorController(
        IOrderAggregationService aggregationService,
        ILogger<AggregatorController> logger)
    {
        _aggregationService = aggregationService ?? throw new ArgumentNullException(nameof(aggregationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Отримати повну агреговану картку замовлення/угоди (Order/Deal + Catalog Products + Reviews/Activities).
    /// Підтримує часткові відмови (graceful degradation) із записом попереджень у Warnings.
    /// </summary>
    [HttpGet("orders/{id:long}/full")]
    [HttpGet("deals/{id:long}/full")]
    [ProducesResponseType(typeof(FullOrderDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FullOrderDto>> GetFullOrder(long id, CancellationToken ct)
    {
        _logger.LogInformation("Aggregating full order/deal card for {OrderId}", id);

        var result = await _aggregationService.GetFullOrderAsync(id, ct);
        if (result is null)
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Замовлення / угоду не знайдено",
                Detail = $"Угоду або замовлення з ID {id} не знайдено у сервісі замовлень.",
                Instance = HttpContext.Request.Path
            });
        }

        return Ok(result);
    }
}
