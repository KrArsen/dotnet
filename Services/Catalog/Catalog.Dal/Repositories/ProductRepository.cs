using Catalog.Dal.Context;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class ProductRepository : GenericRepository<Product>, IProductRepository
{
    public ProductRepository(AppDbContext context) : base(context)
    {
    }

    // Eager Loading: єдиний SQL-запит з підтягуванням 1:1 деталей, 1:N картинок, 1:N історії цін та M:N категорій
    public async Task<Product?> GetWithDetailsAsync(long id, CancellationToken ct = default)
    {
        return await DbSet
            .Include(p => p.Detail)
            .Include(p => p.Images)
            .Include(p => p.PriceHistories)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<bool> ExistsBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return false;

        var normalized = sku.Trim().ToUpperInvariant();
        return await DbSet.AnyAsync(p => p.Sku == normalized, ct);
    }

    public async Task<Product?> GetBySkuAsync(string sku, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sku))
            return null;

        var normalized = sku.Trim().ToUpperInvariant();
        return await DbSet
            .Include(p => p.Detail)
            .Include(p => p.Images)
            .Include(p => p.PriceHistories)
            .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
            .FirstOrDefaultAsync(p => p.Sku == normalized, ct);
    }
}
