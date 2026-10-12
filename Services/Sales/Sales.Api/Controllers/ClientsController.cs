using Microsoft.AspNetCore.Mvc;
using Sales.Bll.Dto;
using Sales.Bll.Services;
using Sales.Domain.Exceptions;

namespace Sales.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class ClientsController : ControllerBase
{
    private readonly IClientService _clientService;

    public ClientsController(IClientService clientService)
    {
        _clientService = clientService ?? throw new ArgumentNullException(nameof(clientService));
    }

    /// <summary>
    /// Отримати список клієнтів із пагінацією та пошуком
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ClientDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ClientDto>>> GetPage(
        [FromQuery] ClientQueryDto query,
        CancellationToken ct)
    {
        var result = await _clientService.GetPageAsync(query, ct);
        return Ok(result);
    }

    /// <summary>
    /// Отримати дані клієнта за ідентифікатором
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ClientDto>> GetById(long id, CancellationToken ct)
    {
        var client = await _clientService.GetByIdAsync(id, ct);
        if (client == null)
        {
            throw new NotFoundException($"Клієнта з ID {id} не знайдено.");
        }

        return Ok(client);
    }

    /// <summary>
    /// Створити нового клієнта (чистий ADO.NET)
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientDto>> Create(
        [FromBody] CreateClientDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var created = await _clientService.CreateAsync(dto, user ?? "system", ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Оновити дані клієнта (з перевіркою версії для оптимістичного блокування)
    /// </summary>
    [HttpPut("{id:long}")]
    [ProducesResponseType(typeof(ClientDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ClientDto>> Update(
        long id,
        [FromBody] UpdateClientDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var updated = await _clientService.UpdateAsync(id, dto, user ?? "system", ct);
        return Ok(updated);
    }

    /// <summary>
    /// М'яке видалення клієнта (Soft Delete)
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
        await _clientService.SoftDeleteAsync(id, user ?? "system", ct);
        return NoContent();
    }
}
