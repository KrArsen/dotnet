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

        _logger.LogInformation("Створено позицію каталогу '{Name}' (ID: {Id}, SKU: {Sku})", product.Name, product.Id, product.Sku);
        return _mapper.Map<ProductDto>(product);
    }

    public async Task UpdatePriceAsync(long id, UpdatePriceDto dto, string updatedBy = "system", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var product = await _uow.Products.GetWithDetailsAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        // Інкапсульоване доменне правило зміни ціни (перевіряє > 0 та фіксує PriceHistory)
        product.ChangePrice(dto.NewPrice, updatedBy, dto.Reason);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Змінено ціну товару ID {Id} на {NewPrice}", id, dto.NewPrice);
    }

    public async Task RemoveStockAsync(long id, RemoveStockDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        // Інкапсульоване доменне правило (перевіряє залишок, кидає DomainException)
        product.RemoveStock(dto.Quantity);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Списано залишок товару ID {Id} на кількість {Quantity}. Новий залишок: {Stock}", id, dto.Quantity, product.StockQuantity);
    }

    public async Task AddStockAsync(long id, AddStockDto dto, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        product.AddStock(dto.Quantity);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Поповнено залишок товару ID {Id} на кількість {Quantity}. Новий залишок: {Stock}", id, dto.Quantity, product.StockQuantity);
    }

    public async Task DeleteAsync(long id, string deletedBy = "system", CancellationToken ct = default)
    {
        var product = await _uow.Products.GetByIdAsync(id, ct);
        if (product == null || product.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Product), id);
        }

        product.MarkDeleted(deletedBy);

        _uow.Products.Update(product);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("М'яко видалено товар ID {Id}", id);
    }

    public async Task<IReadOnlyList<ProductDto>> GetByCategoryAsync(long categoryId, CancellationToken ct = default)
    {
        // Застосування альтернативної специфікації ProductsByCategorySpec через загальний метод репозиторію
        var spec = new ProductsByCategorySpec(categoryId);
        var products = await _uow.Products.ListAsync(spec, ct);
        return _mapper.Map<IReadOnlyList<ProductDto>>(products);
    }
}
