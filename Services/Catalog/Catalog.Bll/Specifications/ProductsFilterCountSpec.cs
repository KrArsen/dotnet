using Ardalis.Specification;
using Catalog.Bll.DTOs;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

public class ProductsFilterCountSpec : Specification<Product>
{
    public ProductsFilterCountSpec(ProductQuery query)
    {
        Query.Where(p => !p.IsDeleted);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            Query.Where(p => p.Name.Contains(search) || p.Sku.Contains(search));
        }

        if (query.MinPrice.HasValue)
        {
            Query.Where(p => p.Price >= query.MinPrice.Value);
        }

        if (query.MaxPrice.HasValue)
        {
            Query.Where(p => p.Price <= query.MaxPrice.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.ItemType))
        {
            Query.Where(p => p.ItemType == query.ItemType);
        }

        if (query.CategoryId.HasValue)
        {
            Query.Where(p => p.ProductCategories.Any(pc => pc.CategoryId == query.CategoryId.Value));
        }
    }
}
