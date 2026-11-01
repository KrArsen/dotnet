using System.Diagnostics;
using AutoMapper;
using Catalog.Bll.DTOs;
using Catalog.Bll.Specifications;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

public class ProductService : IProductService
{
    private static readonly ActivitySource Source = new("Catalog.Api");

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<ProductService> _logger;

    public ProductService(IUnitOfWork uow, IMapper mapper, ILogger<ProductService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PagedResult<ProductDto>> GetPagedAsync(ProductQuery query, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("GetProductsPaged");
        activity?.SetTag("catalog.page", query.Page);
        activity?.SetTag("catalog.pageSize", query.PageSize);

        // Нормалізація пагінації
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var normalizedQuery = query with { Page = page, PageSize = pageSize };

        // 1. Окремий CountAsync за специфікацією для точного підрахунку всіх записів у БД (без Skip/Take)
        var countSpec = new ProductsFilterCountSpec(normalizedQuery);
        var totalCount = await _uow.Products.CountAsync(countSpec, ct);

        // 2. Отримання сторінки записів через SQL з фільтрацією, сортуванням та Skip/Take
        var filterSpec = new ProductsFilterSpec(normalizedQuery);
        var products = await _uow.Products.ListAsync(filterSpec, ct);

        var dtos = _mapper.Map<IReadOnlyList<ProductDto>>(products);
        return new PagedResult<ProductDto>(dtos, totalCount, page, pageSize);
    }

    public async Task<ProductDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("GetProductById");
        activity?.SetTag("product.id", id);

        var product = await _uow.Products.GetWithDetailsAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        return _mapper.Map<ProductDto>(product);
    }

    public async Task<ProductDto> CreateAsync(CreateProductDto dto, string createdBy = "system", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        using var activity = Source.StartActivity("CreateProduct");
        activity?.SetTag("product.sku", dto.Sku);
        activity?.SetTag("product.itemType", dto.ItemType);

        // Перевірка унікальності SKU (бізнес-правило BLL)
        var skuExists = await _uow.Products.ExistsBySkuAsync(dto.Sku, ct);
        if (skuExists)
        {
            throw new ConflictException($"Позиція з артикулом (SKU) '{dto.Sku}' вже існує в каталозі.");
        }

        // Створення агрегату Product із захищеними інваріантами
        var product = new Product(
            dto.Sku,
            dto.Name,
            dto.ItemType,
            dto.Price,
            dto.StockQuantity,
            createdBy);

        if (!string.IsNullOrWhiteSpace(dto.Description))
        {
            var detail = new ProductDetail(
                product,
                dto.Description,
                dto.Unit ?? "послуга",
                dto.DurationMinutes,
                dto.TaxRate ?? 20.00m,
                createdBy);

            product.SetDetail(detail);
        }

        if (dto.CategoryIds != null && dto.CategoryIds.Count > 0)
        {
            foreach (var catId in dto.CategoryIds.Distinct())
            {
                var category = await _uow.Categories.GetByIdAsync(catId, ct);
                if (category != null && !category.IsDeleted)
                {
                    product.AssignCategory(category);
                }
            }
        }

        await _uow.Products.AddAsync(product, ct);
        await _uow.SaveChangesAsync(ct);

        activity?.SetTag("product.id", product.Id);
        _logger.LogInformation(
            "Product {ProductId} created (SKU: {Sku}, Name: {Name}, Price: {Amount}, Stock: {Stock}, Status: {Status})",
            product.Id,
            product.Sku,
            product.Name,
            product.Price,
            product.StockQuantity,
            product.IsActive ? "Active" : "Inactive");

        return _mapper.Map<ProductDto>(product);
    }

    public async Task UpdatePriceAsync(long id, UpdatePriceDto dto, string updatedBy = "system", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        using var activity = Source.StartActivity("UpdateProductPrice");
        activity?.SetTag("product.id", id);
        activity?.SetTag("product.newPrice", dto.NewPrice);

        var product = await _uow.Products.GetWithDetailsAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        var oldPrice = product.Price;

        // Інкапсульоване доменне правило зміни ціни (перевіряє > 0 та фіксує PriceHistory)
        product.ChangePrice(dto.NewPrice, updatedBy, dto.Reason);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Product {ProductId} price updated from {OldAmount} to {Amount}, Status {Status}",
            id,
            oldPrice,
            dto.NewPrice,
            product.IsActive ? "Active" : "Inactive");
    }

    public async Task RemoveStockAsync(long id, RemoveStockDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        using var activity = Source.StartActivity("RemoveProductStock");
        activity?.SetTag("product.id", id);
        activity?.SetTag("product.quantity", dto.Quantity);

        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        // Інкапсульоване доменне правило (перевіряє залишок, кидає DomainException)
        product.RemoveStock(dto.Quantity);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Product {ProductId} stock decreased by {Quantity}. Remaining stock: {Stock}, Status {Status}",
            id,
            dto.Quantity,
            product.StockQuantity,
            product.IsActive ? "Active" : "Inactive");
    }

    public async Task AddStockAsync(long id, AddStockDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        using var activity = Source.StartActivity("AddProductStock");
        activity?.SetTag("product.id", id);
        activity?.SetTag("product.quantity", dto.Quantity);

        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        product.AddStock(dto.Quantity);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Product {ProductId} stock increased by {Quantity}. New stock: {Stock}, Status {Status}",
            id,
            dto.Quantity,
            product.StockQuantity,
            product.IsActive ? "Active" : "Inactive");
    }

    public async Task DeleteAsync(long id, string deletedBy = "system", CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("DeleteProduct");
        activity?.SetTag("product.id", id);

        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        product.MarkDeleted(deletedBy);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Product {ProductId} soft deleted, Status {Status}", id, "Deleted");
    }

    public async Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        // Застосування альтернативної специфікації ProductsByCategorySpec через загальний метод репозиторію
        var spec = new ProductsByCategorySpec(categoryId);
        var products = await _uow.Products.ListAsync(spec, ct);
        return _mapper.Map<IReadOnlyList<ProductDto>>(products);
    }
}
