using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IProductRepository : IGenericRepository<Product>
{
    // Eager Loading: єдиний запит із підвантаженням усіх зв'язаних сутностей
    Task<Product?> GetWithDetailsAsync(long id, CancellationToken ct = default);

    Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default);

    Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default);
}
