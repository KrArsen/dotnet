using Sales.Domain.Models;

namespace Sales.Dal.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    /// <summary>
    /// Пошук активних товарів/послуг за назвою з екрануванням спецсимволів LIKE
    /// </summary>
    Task<IReadOnlyList<Product>> GetActiveByNameAsync(string name, CancellationToken ct = default);

    /// <summary>
    /// Реплікація нової або оновленої позиції каталогу з інтеграційної події Catalog.Api (із захистом від out-of-order).
    /// Не змінює історичні ціни в dbo.DealItems.UnitPrice.
    /// </summary>
    Task UpsertSnapshotAsync(
        long productId,
        string productName,
        decimal price,
        bool isActive,
        DateTime occurredAt,
        CancellationToken ct = default);

    /// <summary>
    /// Оновлення локальної копії ціни та назви в dbo.Products на основі події ProductPriceChangedEvent / ProductRenamedEvent.
    /// </summary>
    Task UpdateSnapshotAsync(
        long productId,
        string productName,
        decimal price,
        DateTime occurredAt,
        CancellationToken ct = default);

    /// <summary>
    /// Деактивація локальної копії позиції в dbo.Products на основі події ProductDeletedEvent.
    /// </summary>
    Task DeactivateSnapshotAsync(
        long productId,
        DateTime occurredAt,
        CancellationToken ct = default);
}
