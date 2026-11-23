using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Auth.Api.Contracts;
using Auth.Api.Data;
using Auth.Api.Models;
using Auth.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Auth.Api.Controllers;

[ApiController]
[Route("auth")]
[Produces("application/json")]
public sealed class AuthController(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    ApplicationDbContext dbContext,
    IJwtTokenService jwtTokenService,
    IAuthEmailService emailService,
    IOptions<JwtSettings> jwtOptions,
    IHostEnvironment environment,
    ILogger<AuthController> logger) : ControllerBase
{
    public const string RefreshTokenCookieName = "refreshToken";
    private readonly JwtSettings _jwtSettings = jwtOptions.Value;

    /// <summary>POST /auth/register — реєстрація користувача, роль User, токен підтвердження email.</summary>
    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var existingByEmail = await userManager.FindByEmailAsync(request.Email);
        if (existingByEmail is not null)
        {
            return Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Title = "Email already registered",
                Detail = $"User with email '{request.Email}' already exists."
            });
        }

        var user = new ApplicationUser
        {
            UserName = request.Username.Trim(),
            Email = request.Email.Trim(),
            FullName = request.FullName?.Trim(),
            EmailConfirmed = false,
            CreatedAt = DateTime.UtcNow
        };

        var createResult = await userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Registration failed",
                Detail = string.Join("; ", createResult.Errors.Select(e => e.Description))
            });
        }

        // Автоматичне призначення ролі User
        await userManager.AddToRoleAsync(user, "User");

        // Генерація одноразового токена підтвердження email та надсилання листа
        var verificationToken = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailService.SendEmailVerificationAsync(user.Email!, user.Id, verificationToken, ct);

        logger.LogInformation("Registered user {Username} ({UserId}) with role User; verification email sent", user.UserName, user.Id);

        return Created($"/users/{user.Id}", new
        {
            userId = user.Id,
            username = user.UserName,
            email = user.Email,
            message = "Registration successful. Please verify your email before logging in.",
            // У development повертаємо токен для зручного тестування у Postman/Swagger
            verificationToken = environment.IsDevelopment() ? verificationToken : null
        });
    }

    /// <summary>POST /auth/login — вхід користувача, повертає JWT access token у тілі та refresh token у HttpOnly cookie.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByNameAsync(request.UsernameOrEmail)
                   ?? await userManager.FindByEmailAsync(request.UsernameOrEmail);

        if (user is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials",
                Detail = "Invalid username/email or password."
            });
        }

        // Блокування входу для непідтвердженого email
        if (!user.EmailConfirmed)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new ProblemDetails
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Email not verified",
                Detail = "Please verify your email address before logging in."
            });
        }

        var signInResult = await signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (signInResult.IsLockedOut)
        {
            return StatusCode(StatusCodes.Status423Locked, new ProblemDetails
            {
                Status = StatusCodes.Status423Locked,
                Title = "Account locked",
                Detail = "Too many failed login attempts. Try again later."
            });
        }

        if (!signInResult.Succeeded)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid credentials",
                Detail = "Invalid username/email or password."
            });
        }

        var roles = await userManager.GetRolesAsync(user);
        var accessToken = jwtTokenService.GenerateAccessToken(user, roles);
        var refreshToken = jwtTokenService.GenerateRefreshToken(user.Id, GetClientIp());

        dbContext.RefreshTokens.Add(refreshToken);
        await dbContext.SaveChangesAsync(ct);

        SetRefreshTokenCookie(refreshToken.Token, refreshToken.ExpiresAt);

        logger.LogInformation("User {Username} ({UserId}) logged in successfully", user.UserName, user.Id);

        return Ok(new AuthResponse(
            AccessToken: accessToken,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes),
            UserId: user.Id,
            Username: user.UserName ?? string.Empty,
            Email: user.Email ?? string.Empty,
            Roles: roles.ToList()));
    }

    /// <summary>POST /auth/refresh-token — ротація refresh token (старий відкликається, новий створюється у HttpOnly cookie).</summary>
    [HttpPost("refresh-token")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> RefreshToken([FromBody] RefreshTokenRequest? request, CancellationToken ct)
    {
        var tokenValue = Request.Cookies[RefreshTokenCookieName] ?? request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh token missing",
                Detail = "No refresh token found in HttpOnly cookie or request body."
            });
        }

        var existingToken = await dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.Token == tokenValue, ct);

        if (existingToken is null)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Invalid refresh token",
                Detail = "Refresh token not found."
            });
        }

        // Виявлення повторного використання відкликаного токена -> відкликаємо всі активні токени користувача
        if (existingToken.IsRevoked)
        {
            await RevokeAllUserTokensAsync(existingToken.UserId, "Attempted reuse of revoked token", ct);
            logger.LogWarning("Revoked refresh token reuse detected for User {UserId}; all active tokens revoked", existingToken.UserId);
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Token reuse detected",
                Detail = "Refresh token has already been revoked. Please log in again."
            });
        }

        if (existingToken.IsExpired)
        {
            return Unauthorized(new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Refresh token expired",
                Detail = "Refresh token has expired. Please log in again."
            });
        }

        // Ротація: відкликаємо старий токен і створюємо новий
        var newRefreshToken = jwtTokenService.GenerateRefreshToken(existingToken.UserId, GetClientIp());
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedReason = "Rotated";
        existingToken.ReplacedByToken = newRefreshToken.Token;

        dbContext.RefreshTokens.Add(newRefreshToken);
        await dbContext.SaveChangesAsync(ct);

        var roles = await userManager.GetRolesAsync(existingToken.User);
        var newAccessToken = jwtTokenService.GenerateAccessToken(existingToken.User, roles);

        SetRefreshTokenCookie(newRefreshToken.Token, newRefreshToken.ExpiresAt);

        logger.LogInformation("Rotated refresh token for User {UserId}", existingToken.UserId);

        return Ok(new AuthResponse(
            AccessToken: newAccessToken,
            ExpiresAtUtc: DateTime.UtcNow.AddMinutes(_jwtSettings.AccessTokenMinutes),
            UserId: existingToken.User.Id,
            Username: existingToken.User.UserName ?? string.Empty,
            Email: existingToken.User.Email ?? string.Empty,
            Roles: roles.ToList()));
    }

    /// <summary>POST /auth/revoke-token — відкликання активного refresh token.</summary>
    [HttpPost("revoke-token")]
    [Authorize]
    public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest? request, CancellationToken ct)
    {
        var tokenValue = Request.Cookies[RefreshTokenCookieName] ?? request?.RefreshToken;
        if (string.IsNullOrWhiteSpace(tokenValue))
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Token required" });
        }

        var token = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenValue, ct);
        if (token is null || !token.IsActive)
        {
            return NotFound(new ProblemDetails { Status = 404, Title = "Active refresh token not found" });
        }

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedReason = "Revoked by user";
        await dbContext.SaveChangesAsync(ct);

        return Ok(new { message = "Refresh token revoked successfully." });
    }

    /// <summary>POST /auth/logout — вихід користувача, відкликання токена та очищення HttpOnly cookie.</summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshTokenRequest? request, CancellationToken ct)
    {
        var tokenValue = Request.Cookies[RefreshTokenCookieName] ?? request?.RefreshToken;
        if (!string.IsNullOrWhiteSpace(tokenValue))
        {
            var token = await dbContext.RefreshTokens.FirstOrDefaultAsync(t => t.Token == tokenValue, ct);
            if (token is not null && token.IsActive)
            {
                token.RevokedAt = DateTime.UtcNow;
                token.RevokedReason = "Logout";
                await dbContext.SaveChangesAsync(ct);
            }
        }

        Response.Cookies.Delete(RefreshTokenCookieName);
        return Ok(new { message = "Logged out successfully." });
    }

    /// <summary>POST /auth/verify-email — підтвердження email за одноразовим токеном.</summary>
    [HttpPost("verify-email")]
    [AllowAnonymous]
    public async Task<IActionResult> VerifyEmail([FromBody] VerifyEmailRequest request)
    {
        var user = await userManager.FindByIdAsync(request.UserId);
        if (user is null)
        {
            return NotFound(new ProblemDetails { Status = 404, Title = "User not found" });
        }

        var result = await userManager.ConfirmEmailAsync(user, request.Token);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Email verification failed",
                Detail = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }

        logger.LogInformation("Email verified for User {UserId}", user.Id);
        return Ok(new { message = "Email verified successfully. You can now log in." });
    }

    /// <summary>POST /auth/resend-verification — повторне надсилання листа підтвердження email.</summary>
    [HttpPost("resend-verification")]
    [AllowAnonymous]
    public async Task<IActionResult> ResendVerification([FromBody] ResendVerificationRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null || user.EmailConfirmed)
        {
            return Ok(new { message = "If the account exists and is unverified, a verification email has been sent." });
        }

        var token = await userManager.GenerateEmailConfirmationTokenAsync(user);
        await emailService.SendEmailVerificationAsync(user.Email!, user.Id, token, ct);

        return Ok(new
        {
            message = "Verification email resent.",
            verificationToken = environment.IsDevelopment() ? token : null
        });
    }

    /// <summary>POST /auth/forgot-password — запит на скидання пароля (одноразовий токен).</summary>
    [HttpPost("forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return Ok(new { message = "If the email exists, password reset instructions have been sent." });
        }

        var resetToken = await userManager.GeneratePasswordResetTokenAsync(user);
        await emailService.SendPasswordResetAsync(user.Email!, resetToken, ct);

        return Ok(new
        {
            message = "Password reset instructions sent.",
            resetToken = environment.IsDevelopment() ? resetToken : null
        });
    }

    /// <summary>POST /auth/reset-password — скидання пароля за токеном + відкликання всіх refresh tokens.</summary>
    [HttpPost("reset-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        var user = await userManager.FindByEmailAsync(request.Email);
        if (user is null)
        {
            return BadRequest(new ProblemDetails { Status = 400, Title = "Invalid reset request" });
        }

        var result = await userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password reset failed",
                Detail = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }

        // Після скидання пароля відкликаємо всі активні refresh tokens користувача
        await RevokeAllUserTokensAsync(user.Id, "Password reset", ct);
        logger.LogInformation("Password reset completed for User {UserId}; all refresh tokens revoked", user.Id);

        return Ok(new { message = "Password has been reset and all existing sessions revoked." });
    }

    /// <summary>POST /auth/change-password — зміна пароля автентифікованим користувачем.</summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request, CancellationToken ct)
    {
        var userId = User.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return NotFound();
        }

        var result = await userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Password change failed",
                Detail = string.Join("; ", result.Errors.Select(e => e.Description))
            });
        }

        await RevokeAllUserTokensAsync(user.Id, "Password changed", ct);
        return Ok(new { message = "Password changed successfully." });
    }

    private void SetRefreshTokenCookie(string token, DateTime expiresAt)
    {
        Response.Cookies.Append(RefreshTokenCookieName, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = !environment.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            Expires = expiresAt
        });
    }

    private async Task RevokeAllUserTokensAsync(string userId, string reason, CancellationToken ct)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(t => t.UserId == userId && t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync(ct);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = DateTime.UtcNow;
            token.RevokedReason = reason;
        }

        if (activeTokens.Count > 0)
        {
            await dbContext.SaveChangesAsync(ct);
        }
    }

    private string? GetClientIp() =>
        HttpContext.Connection.RemoteIpAddress?.ToString();
}
