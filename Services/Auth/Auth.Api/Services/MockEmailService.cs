namespace Auth.Api.Services;

public interface IAuthEmailService
{
    Task SendEmailVerificationAsync(string email, string userId, string token, CancellationToken ct = default);
    Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default);
}

/// <summary>
/// Mock Email Service для development-середовища: логує одноразові токени підтвердження email та скидання пароля.
/// </summary>
public sealed class MockEmailService(ILogger<MockEmailService> logger) : IAuthEmailService
{
    public Task SendEmailVerificationAsync(string email, string userId, string token, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MOCK EMAIL] Email verification sent to {Email} (UserId: {UserId}). VerificationToken: {Token}",
            email,
            userId,
            token);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetAsync(string email, string token, CancellationToken ct = default)
    {
        logger.LogInformation(
            "[MOCK EMAIL] Password reset link sent to {Email}. ResetToken: {Token}",
            email,
            token);
        return Task.CompletedTask;
    }
}
