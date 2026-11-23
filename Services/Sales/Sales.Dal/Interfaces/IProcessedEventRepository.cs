namespace Sales.Dal.Interfaces;

/// <summary>
/// Репозиторій оброблених інтеграційних подій для забезпечення ідемпотентності споживачів (EventId PK).
/// Запис виконується в одній транзакції UnitOfWork разом із бізнес-змінами.
/// </summary>
public interface IProcessedEventRepository
{
    /// <summary>
    /// Атомарно намагається зареєструвати EventId у dbo.ProcessedEvents.
    /// Повертає true, якщо подія нова; false — якщо EventId вже оброблено (порушення PK 2627/2601).
    /// </summary>
    Task<bool> TryAddAsync(Guid eventId, string eventType, CancellationToken ct = default);
}
