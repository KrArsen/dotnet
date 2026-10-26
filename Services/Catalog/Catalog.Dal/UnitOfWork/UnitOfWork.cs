using Catalog.Dal.Context;
using Catalog.Dal.Interfaces;
using Catalog.Dal.Repositories;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Catalog.Dal.UnitOfWork;

public class UnitOfWork : IUnitOfWork
{
    private readonly AppDbContext _context;
    private readonly ILogger<UnitOfWork>? _logger;
    private IDbContextTransaction? _transaction;

    private IProductRepository? _products;
    private ICategoryRepository? _categories;
    private IProductCategoryRepository? _productCategories;
    private IGenericRepository<ProductDetail>? _productDetails;
    private IGenericRepository<ProductImage>? _productImages;

    public UnitOfWork(AppDbContext context, ILogger<UnitOfWork>? logger = null)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger;
    }

    public IProductRepository Products =>
        _products ??= new ProductRepository(_context);

    public ICategoryRepository Categories =>
        _categories ??= new CategoryRepository(_context);

    public IProductCategoryRepository ProductCategories =>
        _productCategories ??= new ProductCategoryRepository(_context);

    public IGenericRepository<ProductDetail> ProductDetails =>
        _productDetails ??= new GenericRepository<ProductDetail>(_context);

    public IGenericRepository<ProductImage> ProductImages =>
        _productImages ??= new GenericRepository<ProductImage>(_context);

    // Єдине місце збереження змін у базі даних
    public async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _context.SaveChangesAsync(ct);
    }

    public async Task BeginTransactionAsync(CancellationToken ct = default)
    {
        if (_transaction != null)
            return;

        _logger?.LogDebug("Початок транзакції CatalogDb");
        _transaction = await _context.Database.BeginTransactionAsync(ct);
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
            return;

        try
        {
            await _context.SaveChangesAsync(ct);
            await _transaction.CommitAsync(ct);
            _logger?.LogDebug("Транзакцію CatalogDb успішно зафіксовано");
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction == null)
            return;

        try
        {
            await _transaction.RollbackAsync(ct);
            _logger?.LogWarning("Транзакцію CatalogDb відкочено");
        }
        finally
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction != null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        await _context.DisposeAsync();
        GC.SuppressFinalize(this);
    }
}
