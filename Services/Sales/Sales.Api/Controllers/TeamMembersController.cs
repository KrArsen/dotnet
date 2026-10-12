using Microsoft.AspNetCore.Mvc;
using Sales.Bll.Dto;
using Sales.Bll.Services;
using Sales.Domain.Exceptions;

namespace Sales.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class TeamMembersController : ControllerBase
{
    private readonly ITeamMemberService _teamMemberService;

    public TeamMembersController(ITeamMemberService teamMemberService)
    {
        _teamMemberService = teamMemberService ?? throw new ArgumentNullException(nameof(teamMemberService));
    }

    /// <summary>
    /// Отримати перелік співробітників компанії
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<TeamMemberDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<TeamMemberDto>>> GetAll(CancellationToken ct)
    {
        var members = await _teamMemberService.GetAllAsync(ct);
        return Ok(members);
    }

    /// <summary>
    /// Отримати дані співробітника за ідентифікатором
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(TeamMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMemberDto>> GetById(long id, CancellationToken ct)
    {
        var member = await _teamMemberService.GetByIdAsync(id, ct);
        if (member == null)
        {
            throw new NotFoundException($"Співробітника з ID {id} не знайдено.");
        }

        return Ok(member);
    }

    /// <summary>
    /// Отримати співробітника за електронною поштою
    /// </summary>
    [HttpGet("by-email")]
    [ProducesResponseType(typeof(TeamMemberDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<TeamMemberDto>> GetByEmail([FromQuery] string email, CancellationToken ct)
    {
        var member = await _teamMemberService.GetByEmailAsync(email ?? string.Empty, ct);
        if (member == null)
        {
            throw new NotFoundException($"Співробітника з email '{email}' не знайдено.");
        }

        return Ok(member);
    }
}
