using Ardalis.Specification;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

public class ProductsByCategorySpec : Specification<Product>
{
    public ProductsByCategorySpec(long categoryId)
    {
        Query.Where(p => !p.IsDeleted && p.ProductCategories.Any(pc => pc.CategoryId == categoryId))
             .Include(p => p.Detail)
             .Include(p => p.Images)
             .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category)
             .OrderBy(p => p.Name);
    }
}
