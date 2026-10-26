using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IProductCategoryRepository : IGenericRepository<ProductCategory>
{
    // LINQ to Entities M:N запит через проміжну таблицю та її власне поле AddedAt
    Task<IReadOnlyList<Product>> GetRecentlyAddedAsync(string categoryName, DateTime since, CancellationToken ct = default);
}
