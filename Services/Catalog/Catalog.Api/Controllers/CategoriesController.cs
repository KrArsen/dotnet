using Catalog.Bll.DTOs;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/categories")]
[Produces("application/json")]
public class CategoriesController : ControllerBase
{
    private readonly ICategoryService _categoryService;
    private readonly IValidator<CreateCategoryDto> _createValidator;

    public CategoriesController(
        ICategoryService categoryService,
        IValidator<CreateCategoryDto> createValidator)
    {
        _categoryService = categoryService ?? throw new ArgumentNullException(nameof(categoryService));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
    }

    /// <summary>
    /// Отримати перелік усіх активних категорій послуг та товарів
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> GetAll(CancellationToken ct)
    {
        var categories = await _categoryService.GetAllAsync(ct);
        return Ok(categories);
    }

    /// <summary>
    /// Отримати категорію за ID
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryDto>> GetById(long id, CancellationToken ct)
    {
        var category = await _categoryService.GetByIdAsync(id, ct);
        return Ok(category);
    }

    /// <summary>
    /// Отримати категорію разом із товарами за допомогою Explicit Loading (2 окремих SQL-запити)
    /// </summary>
    [HttpGet("{id:long}/products")]
    [ProducesResponseType(typeof(CategoryWithProductsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CategoryWithProductsDto>> GetWithProducts(long id, CancellationToken ct)
    {
        var result = await _categoryService.GetWithProductsExplicitAsync(id, ct);
        return Ok(result);
    }

    /// <summary>
    /// Отримати нещодавно додані товари певної категорії за допомогою M:N запиту через власне поле AddedAt
    /// </summary>
    [HttpGet("recent-products")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetRecentlyAdded(
        [FromQuery] string categoryName,
        [FromQuery] DateTime? since,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Некоректні параметри",
                Detail = "Параметр categoryName є обов'язковим."
            });
        }

        var sinceDate = since ?? DateTime.UtcNow.AddMonths(-12);
        var products = await _categoryService.GetRecentlyAddedProductsAsync(categoryName, sinceDate, ct);
        return Ok(products);
    }

    /// <summary>
    /// Створити нову категорію каталогу
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CategoryDto>> Create(
        [FromBody] CreateCategoryDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var validationResult = await _createValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Помилка валідації категорії"
            });
        }

        var created = await _categoryService.CreateAsync(dto, user ?? "system", ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }
}
