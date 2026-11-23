using System.ComponentModel.DataAnnotations;

namespace Auth.Api.Contracts;

public record RegisterRequest(
    [Required, MinLength(3), MaxLength(100)] string Username,
    [Required, EmailAddress] string Email,
    [Required, MinLength(8)] string Password,
    string? FullName = null);

public record LoginRequest(
    [Required] string UsernameOrEmail,
    [Required] string Password);

public record AuthResponse(
    string AccessToken,
    DateTime ExpiresAtUtc,
    string UserId,
    string Username,
    string Email,
    IReadOnlyList<string> Roles);

public record RefreshTokenRequest(string? RefreshToken = null);

public record VerifyEmailRequest(
    [Required] string UserId,
    [Required] string Token);

public record ResendVerificationRequest([Required, EmailAddress] string Email);

public record ForgotPasswordRequest([Required, EmailAddress] string Email);

public record ResetPasswordRequest(
    [Required, EmailAddress] string Email,
    [Required] string Token,
    [Required, MinLength(8)] string NewPassword);

public record ChangePasswordRequest(
    [Required] string CurrentPassword,
    [Required, MinLength(8)] string NewPassword);

public record UpdateUserRequest(
    string? FullName,
    [EmailAddress] string? Email,
    string? PhoneNumber);

public record AssignRoleRequest([Required] string Role);

public record UserDto(
    string Id,
    string Username,
    string Email,
    string? FullName,
    bool EmailConfirmed,
    DateTime CreatedAt,
    IReadOnlyList<string> Roles);

public record RefreshTokenDto(
    long Id,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? RevokedAt,
    string? RevokedReason,
    string? CreatedByIp,
    bool IsActive);
