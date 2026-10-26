using AutoMapper;
using Catalog.Bll.DTOs;
using Catalog.Dal.Interfaces;
using Catalog.Domain.Entities;
using Catalog.Domain.Exceptions;
using Microsoft.Extensions.Logging;

namespace Catalog.Bll.Services;

public class CategoryService : ICategoryService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<CategoryService> _logger;

    public CategoryService(IUnitOfWork uow, IMapper mapper, ILogger<CategoryService> logger)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<CategoryDto>> GetAllAsync(CancellationToken ct = default)
    {
        var categories = await _uow.Categories.GetAllAsync(ct);
        return _mapper.Map<IReadOnlyList<CategoryDto>>(categories.Where(c => !c.IsDeleted).ToList());
    }

    public async Task<CategoryDto> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetByIdAsync(id, ct);
        if (category == null || category.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Category), id);
        }

        return _mapper.Map<CategoryDto>(category);
    }

    // Демонстрація Explicit Loading: виклик специфічного методу репозиторію, який робить 2 окремих SQL-запити
    public async Task<CategoryWithProductsDto> GetWithProductsExplicitAsync(long id, CancellationToken ct = default)
    {
        var category = await _uow.Categories.GetWithProductsAsync(id, ct);
        if (category == null || category.IsDeleted)
        {
            throw new EntityNotFoundException(nameof(Category), id);
        }

        return _mapper.Map<CategoryWithProductsDto>(category);
    }

    // Демонстрація LINQ M:N: отримання нещодавно доданих товарів за назвою категорії
    public async Task<IReadOnlyList<ProductDto>> GetRecentlyAddedProductsAsync(string categoryName, DateTime since, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            throw new ArgumentException("Назва категорії не може бути порожньою.", nameof(categoryName));
        }

        var products = await _uow.ProductCategories.GetRecentlyAddedAsync(categoryName.Trim(), since, ct);
        return _mapper.Map<IReadOnlyList<ProductDto>>(products);
    }

    public async Task<CategoryDto> CreateAsync(CreateCategoryDto dto, string createdBy = "system", CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        var exists = await _uow.Categories.ExistsByNameAsync(dto.Name, ct);
        if (exists)
        {
            throw new ConflictException($"Категорія з назвою '{dto.Name}' вже існує.");
        }

        var category = new Category(dto.Name, dto.Description, createdBy);
        await _uow.Categories.AddAsync(category, ct);
        await _uow.SaveChangesAsync(ct);

        _logger.LogInformation("Створено категорію '{Name}' (ID: {Id})", category.Name, category.Id);
        return _mapper.Map<CategoryDto>(category);
    }
}
