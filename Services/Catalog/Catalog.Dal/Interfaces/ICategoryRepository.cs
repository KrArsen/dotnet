using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface ICategoryRepository : IGenericRepository<Category>
{
    // Explicit Loading: спочатку вибірка категорії (1-й запит), потім явне завантаження товарів через Entry (2-й запит)
    Task<Category?> GetWithProductsAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default);
}
