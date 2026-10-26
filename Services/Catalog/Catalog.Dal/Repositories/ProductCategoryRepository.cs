using Catalog.Dal.Context;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductCategoryRepository : GenericRepository<ProductCategory>, IProductCategoryRepository
{
    public ProductCategoryRepository(AppDbContext context) : base(context)
    {
    }

    // LINQ to Entities запит M:N з фільтрацією за категорією та власним полем AddedAt
    public async Task<IReadOnlyList<Product>> GetRecentlyAddedAsync(string categoryName, DateTime since, CancellationToken ct = default)
    {
        return await DbSet
            .AsNoTracking()
            .Where(pc => pc.Category.Name == categoryName && pc.AddedAt >= since && !pc.Product.IsDeleted)
            .OrderByDescending(pc => pc.AddedAt)
            .Select(pc => pc.Product)
            .ToListAsync(ct);
    }
}
