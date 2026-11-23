namespace Catalog.Domain.Entities;

/// <summary>
/// Запис обробленої інтеграційної події для забезпечення ідемпотентності споживачів (EventId PK).
/// </summary>
public sealed class ProcessedEvent
{
    public Guid EventId { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public DateTime ProcessedAt { get; private set; } = DateTime.UtcNow;

    private ProcessedEvent()
    {
    }

    public ProcessedEvent(Guid eventId, string eventType)
    {
        EventId = eventId;
        EventType = eventType;
        ProcessedAt = DateTime.UtcNow;
    }
}

/// <summary>
/// Запис патерну Transactional Outbox для надійної публікації доменних подій Каталогу.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string EventType { get; private set; } = string.Empty;
    public string Exchange { get; private set; } = string.Empty;
    public string RoutingKey { get; private set; } = string.Empty;
    public string Payload { get; private set; } = string.Empty;
    public string? CorrelationId { get; private set; }
    public DateTime OccurredAt { get; private set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; private set; }

    private OutboxMessage()
    {
    }

    public OutboxMessage(
        Guid id,
        string eventType,
        string exchange,
        string routingKey,
        string payload,
        string? correlationId)
    {
        Id = id;
        EventType = eventType;
        Exchange = exchange;
        RoutingKey = routingKey;
        Payload = payload;
        CorrelationId = correlationId;
        OccurredAt = DateTime.UtcNow;
    }

    public void MarkProcessed() => ProcessedAt = DateTime.UtcNow;
}
