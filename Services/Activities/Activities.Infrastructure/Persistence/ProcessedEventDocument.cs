using MongoDB.Bson.Serialization.Attributes;

namespace Activities.Infrastructure.Persistence;

/// <summary>
/// Документ колекції processed_events у MongoDB для ідемпотентності споживачів RabbitMQ.
/// </summary>
public sealed class ProcessedEventDocument
{
    [BsonId]
    public string Id { get; set; } = string.Empty;

    [BsonElement("eventId")]
    public string EventId { get; set; } = string.Empty;

    [BsonElement("eventType")]
    public string EventType { get; set; } = string.Empty;

    [BsonElement("processedAt")]
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
}
