using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;

namespace Catalog.Dal.Interfaces;

public interface IUnitOfWork : IAsyncDisposable
{
    IProductRepository Products { get; }
    ICategoryRepository Categories { get; }
    IProductCategoryRepository ProductCategories { get; }
    IGenericRepository<ProductDetail> ProductDetails { get; }
    IGenericRepository<ProductImage> ProductImages { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);

    Task BeginTransactionAsync(CancellationToken ct = default);
    Task CommitAsync(CancellationToken ct = default);
    Task RollbackAsync(CancellationToken ct = default);
}
