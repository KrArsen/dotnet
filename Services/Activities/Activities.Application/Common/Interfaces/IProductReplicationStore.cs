namespace Activities.Application.Common.Interfaces;

/// <summary>
/// Контракт ідемпотентної реплікації денормалізованої назви товару/послуги у документах відгуків/активностей
/// (політика узгодженості з Лаб. 01). Реалізується в Activities.Infrastructure поверх MongoDB.
/// </summary>
public interface IProductReplicationStore
{
    /// <summary>
    /// Атомарно реєструє EventId у колекції processed_events.
    /// Повертає true для нової події, false — якщо EventId вже оброблено (DuplicateKey).
    /// </summary>
    Task<bool> TryMarkEventProcessedAsync(Guid eventId, string eventType, CancellationToken ct = default);

    /// <summary>
    /// Оновлює денормалізовану назву товару в усіх документах відгуків/активностей, що посилаються на productId,
    /// із захистом від out-of-order подій. Повертає кількість оновлених документів.
    /// </summary>
    Task<long> UpdateProductNameInReviewsAsync(
        long productId,
        string newProductName,
        DateTime occurredAt,
        CancellationToken ct = default);
}
