using Catalog.Bll.DTOs;

namespace Catalog.Bll.Services;

public interface IProductService
{
    Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct = default);
    Task<ProductDto> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ProductDto> CreateAsync(CreateProductDto dto, string createdBy = "system", CancellationToken ct = default);
    Task UpdatePriceAsync(long id, UpdatePriceDto dto, string updatedBy = "system", CancellationToken ct = default);
    Task RemoveStockAsync(long id, RemoveStockDto dto, CancellationToken ct = default);
    Task AddStockAsync(long id, AddStockDto dto, CancellationToken ct = default);
    Task DeleteAsync(long id, string deletedBy = "system", CancellationToken ct = default);
    Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default);
}
