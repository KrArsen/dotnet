using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Hosting;

namespace Payments.Api.Controllers;

public record PaymentRecord(
    Guid Id,
    long OrderId,
    decimal Amount,
    string Currency,
    string Status,
    string ProcessedBy,
    DateTime CreatedAt);

public record ProcessPaymentRequest(long OrderId, decimal Amount, string Currency = "UAH");
public record RefundPaymentRequest(Guid PaymentId, string Reason);

/// <summary>
/// Демо-сервіс платежів (Payments Service, порт 5003):
/// найчутливіший ресурс системи — доступний ВИКЛЮЧНО ролі admin (Таблиця 7 і Таблиця 10).
/// Ролі manager, user та guest отримують 403 Forbidden на всі endpoints.
/// </summary>
[ApiController]
[Route("payments")]
[Route("api/payments")]
[Produces("application/json")]
public sealed class PaymentsController(ILogger<PaymentsController> logger) : ControllerBase
{
    private static readonly ConcurrentDictionary<Guid, PaymentRecord> Payments = new(
    [
        new KeyValuePair<Guid, PaymentRecord>(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new PaymentRecord(
                Guid.Parse("11111111-1111-1111-1111-111111111111"),
                OrderId: 1,
                Amount: 115000.00m,
                Currency: "UAH",
                Status: "Completed",
                ProcessedBy: "alice.admin",
                CreatedAt: DateTime.UtcNow.AddDays(-2)))
    ]);

    /// <summary>GET /payments — перегляд платежів (permission: payments:read — лише admin).</summary>
    [HttpGet]
    [RequirePermission("payments:read")]
    public ActionResult<IReadOnlyList<PaymentRecord>> GetAll()
    {
        return Ok(Payments.Values.OrderByDescending(p => p.CreatedAt).ToList());
    }

    /// <summary>POST /payments/process — проведення платежу (permission: payments:process — лише admin).</summary>
    [HttpPost("process")]
    [RequirePermission("payments:process")]
    public ActionResult<PaymentRecord> Process([FromBody] ProcessPaymentRequest request)
    {
        var actor = User.FindFirstValue("preferred_username")
                    ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
                    ?? User.Identity?.Name
                    ?? "admin";

        var payment = new PaymentRecord(
            Guid.NewGuid(),
            request.OrderId,
            request.Amount,
            request.Currency,
            "Completed",
            actor,
            DateTime.UtcNow);

        Payments[payment.Id] = payment;
        logger.LogInformation("Payment {PaymentId} processed for Order {OrderId} ({Amount} {Currency}) by {Actor}",
            payment.Id, payment.OrderId, payment.Amount, payment.Currency, actor);

        return Created($"/payments/{payment.Id}", payment);
    }

    /// <summary>POST /payments/refund — повернення коштів (permission: payments:refund — лише admin).</summary>
    [HttpPost("refund")]
    [RequirePermission("payments:refund")]
    public ActionResult<PaymentRecord> Refund([FromBody] RefundPaymentRequest request)
    {
        if (!Payments.TryGetValue(request.PaymentId, out var existing))
        {
            return NotFound(new ProblemDetails
            {
                Status = StatusCodes.Status404NotFound,
                Title = "Payment not found",
                Detail = $"Payment {request.PaymentId} does not exist."
            });
        }

        var refunded = existing with { Status = "Refunded" };
        Payments[existing.Id] = refunded;
        logger.LogInformation("Payment {PaymentId} refunded. Reason: {Reason}", existing.Id, request.Reason);

        return Ok(refunded);
    }
}
