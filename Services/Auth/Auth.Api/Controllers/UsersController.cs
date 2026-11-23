using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Auth.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    ApplicationDbContext dbContext) : ControllerBase
{
    /// <summary>GET /users — список усіх користувачів (тільки Admin).</summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IReadOnlyList<UserDto>>> GetAll(CancellationToken ct)
    {
        var users = await userManager.Users.OrderBy(u => u.UserName).ToListAsync(ct);
        var result = new List<UserDto>(users.Count);

        foreach (var user in users)
        {
            var roles = await userManager.GetRolesAsync(user);
            result.Add(new UserDto(
                user.Id,
                user.UserName ?? string.Empty,
                user.Email ?? string.Empty,
                user.FullName,
                user.EmailConfirmed,
                user.CreatedAt,
                roles.ToList()));
        }

        return Ok(result);
    }

    /// <summary>GET /users/{id} — дані користувача (Owner або Admin).</summary>
    [HttpGet("{id}")]
    public async Task<ActionResult<UserDto>> GetById(string id)
    {
        if (!IsOwnerOrAdmin(id))
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(new UserDto(
            user.Id,
            user.UserName ?? string.Empty,
            user.Email ?? string.Empty,
            user.FullName,
            user.EmailConfirmed,
            user.CreatedAt,
            roles.ToList()));
    }

    /// <summary>PUT /users/{id} — оновлення профілю (Owner або Admin).</summary>
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(string id, [FromBody] UpdateUserRequest request)
    {
        if (!IsOwnerOrAdmin(id))
        {
            return Forbid();
        }

        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (request.FullName is not null)
        {
            user.FullName = request.FullName.Trim();
        }

        if (!string.IsNullOrWhiteSpace(request.Email))
        {
            user.Email = request.Email.Trim();
        }

        if (request.PhoneNumber is not null)
        {
            user.PhoneNumber = request.PhoneNumber.Trim();
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return NoContent();
    }

    /// <summary>DELETE /users/{id} — видалення користувача (тільки Admin).</summary>
    [HttpDelete("{id}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Delete(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var result = await userManager.DeleteAsync(user);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return NoContent();
    }

    /// <summary>GET /users/{id}/refresh-tokens — історія refresh tokens користувача (Owner або Admin).</summary>
    [HttpGet("{id}/refresh-tokens")]
    public async Task<ActionResult<IReadOnlyList<RefreshTokenDto>>> GetRefreshTokens(string id, CancellationToken ct)
    {
        if (!IsOwnerOrAdmin(id))
        {
            return Forbid();
        }

        var tokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == id)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => new RefreshTokenDto(
                t.Id,
                t.CreatedAt,
                t.ExpiresAt,
                t.RevokedAt,
                t.RevokedReason,
                t.CreatedByIp,
                t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(ct);

        return Ok(tokens);
    }

    /// <summary>GET /users/{id}/roles — ролі користувача (Authenticated).</summary>
    [HttpGet("{id}/roles")]
    public async Task<ActionResult<IReadOnlyList<string>>> GetRoles(string id)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var roles = await userManager.GetRolesAsync(user);
        return Ok(roles.ToList());
    }

    /// <summary>POST /users/{id}/roles — призначення ролі користувачу (тільки Admin).</summary>
    [HttpPost("{id}/roles")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AssignRole(string id, [FromBody] AssignRoleRequest request)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        if (!await roleManager.RoleExistsAsync(request.Role))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Role does not exist",
                Detail = $"Role '{request.Role}' does not exist."
            });
        }

        var result = await userManager.AddToRoleAsync(user, request.Role);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return NoContent();
    }

    /// <summary>DELETE /users/{id}/roles/{role} — видалення ролі у користувача (тільки Admin).</summary>
    [HttpDelete("{id}/roles/{role}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var user = await userManager.FindByIdAsync(id);
        if (user is null)
        {
            return NotFound();
        }

        var result = await userManager.RemoveFromRoleAsync(user, role);
        if (!result.Succeeded)
        {
            return BadRequest(result.Errors);
        }

        return NoContent();
    }

    private bool IsOwnerOrAdmin(string targetUserId)
    {
        if (User.IsInRole("Admin"))
        {
            return true;
        }

        var currentUserId = User.FindFirstValue(JwtRegisteredClaimNames.Sub)
                            ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
                            ?? User.FindFirstValue("userId");

        return string.Equals(currentUserId, targetUserId, StringComparison.OrdinalIgnoreCase);
    }
}
