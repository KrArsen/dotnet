using Catalog.Dal.Context;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Repositories;

public class CategoryRepository : GenericRepository<Category>, ICategoryRepository
{
    public CategoryRepository(AppDbContext context) : base(context)
    {
    }

    // Explicit Loading: два незалежних SQL-запити до бази даних
    public async Task<Category?> GetWithProductsAsync(long id, CancellationToken ct = default)
    {
        // 1-й SQL-запит: отримання сутності категорії
        var category = await DbSet.FirstOrDefaultAsync(c => c.Id == id, ct);
        if (category == null)
            return null;

        // 2-й окремий SQL-запит: явне підвантаження зв'язаної колекції через Entry
        await Context.Entry(category)
            .Collection(c => c.ProductCategories)
            .Query()
            .Include(pc => pc.Product)
            .LoadAsync(ct);

        return category;
    }

    public async Task<bool> ExistsByNameAsync(string name, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var normalized = name.Trim();
        return await DbSet.AnyAsync(c => c.Name == normalized, ct);
    }
}
