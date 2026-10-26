using MongoDB.Bson;

namespace Activities.Domain.Common;

/// <summary>
/// Базова сутність для MongoDB-документів з підтримкою оптимістичної конкурентності
/// </summary>
public abstract class BaseEntity
{
    public ObjectId Id { get; protected set; } = ObjectId.GenerateNewId();
    public DateTime CreatedAt { get; protected set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; protected set; }
    public int Version { get; protected set; } = 1;

    /// <summary>
    /// Оновлює мітку часу останньої модифікації сутності
    /// </summary>
    public void Touch()
    {
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Збільшує версію документа для оптимістичного блокування та оновлює мітку часу
    /// </summary>
    public void BumpVersion()
    {
        Version++;
        Touch();
    }
}
