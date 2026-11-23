namespace Platform.EventBus.Events;

/// <summary>
/// Базова незмінна інтеграційна подія (Domain / Integration Event):
/// містить унікальний EventId (для ідемпотентності), OccurredAt (для захисту від out-of-order),
/// CorrelationId (для наскрізного трасування) та версію схеми (SchemaVersion).
/// </summary>
public abstract record IntegrationEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public DateTime OccurredAt { get; init; } = DateTime.UtcNow;
    public string? CorrelationId { get; init; }
    public int SchemaVersion { get; init; } = 1;
}

/// <summary>
/// Подія створення нової позиції в каталозі (публікує Catalog.Api після SaveChangesAsync).
/// </summary>
public sealed record ProductCreatedEvent(
    long ProductId,
    string Sku,
    string ProductName,
    decimal Price,
    bool IsActive) : IntegrationEvent;

/// <summary>
/// Подія зміни ціни товару/послуги в каталозі (публікує Catalog.Api після SaveChangesAsync).
/// Споживач Sales оновлює локальний довідник Products, не змінюючи DealItems.UnitPrice.
/// </summary>
public sealed record ProductPriceChangedEvent(
    long ProductId,
    string ProductName,
    decimal OldPrice,
    decimal NewPrice) : IntegrationEvent;

/// <summary>
/// Подія перейменування товару/послуги в каталозі (публікує Catalog.Api після SaveChangesAsync).
/// Споживач Sales оновлює Products.Name, а Reviews — денормалізоване productName в усіх відгуках.
/// </summary>
public sealed record ProductRenamedEvent(
    long ProductId,
    string OldName,
    string NewName,
    decimal CurrentPrice) : IntegrationEvent;

/// <summary>
/// Подія м'якого видалення (деактивації) товару/послуги в каталозі.
/// </summary>
public sealed record ProductDeletedEvent(
    long ProductId,
    string ProductName) : IntegrationEvent;

/// <summary>
/// Подія зміни статусу замовлення/угоди (публікує Sales.Api після успішного коміту в БД).
/// </summary>
public sealed record OrderStatusChangedEvent(
    long OrderId,
    string DealNumber,
    long ClientId,
    string OldStatus,
    string NewStatus,
    decimal TotalAmount) : IntegrationEvent;
