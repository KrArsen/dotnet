using Ardalis.Specification;
using Catalog.Bll.DTOs;
using Catalog.Domain.Entities;

namespace Catalog.Bll.Specifications;

public class ProductsFilterSpec : Specification<Product>
{
    public ProductsFilterSpec(ProductQuery query)
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

        // Включення зв'язаних сутностей для повного представлення товару
        Query.Include(p => p.Detail)
             .Include(p => p.Images)
             .Include(p => p.ProductCategories)
                .ThenInclude(pc => pc.Category);

        // Безпечне сортування за білим списком полів
        var isDesc = string.Equals(query.SortDir, "desc", StringComparison.OrdinalIgnoreCase);
        var sortBy = query.SortBy?.ToLowerInvariant() ?? "name";

        switch (sortBy)
        {
            case "price":
                if (isDesc) Query.OrderByDescending(p => p.Price);
                else Query.OrderBy(p => p.Price);
                break;
            case "sku":
                if (isDesc) Query.OrderByDescending(p => p.Sku);
                else Query.OrderBy(p => p.Sku);
                break;
            case "createdat":
                if (isDesc) Query.OrderByDescending(p => p.CreatedAt);
                else Query.OrderBy(p => p.CreatedAt);
                break;
            case "name":
            default:
                if (isDesc) Query.OrderByDescending(p => p.Name);
                else Query.OrderBy(p => p.Name);
                break;
        }

        // Пагінація на рівні SQL (OFFSET / FETCH)
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        Query.Skip(skip).Take(pageSize);
    }
}
