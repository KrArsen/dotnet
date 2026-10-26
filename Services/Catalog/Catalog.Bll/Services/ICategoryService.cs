using Catalog.Bll.DTOs;

namespace Catalog.Bll.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default);
    Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<CategoryWithProductsDto> GetWithProductsExplicitAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetRecentlyAddedProductsAsync(string categoryName, DateTime since, CancellationToken ct = default);
    Task<CategoryDto> CreateAsync(CreateCategoryDto dto, string createdBy = "system", CancellationToken ct = default);
}
