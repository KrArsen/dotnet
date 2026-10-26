using System.Text.Json;
using Catalog.Bll.DTOs;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

[ApiController]
[Route("api/products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IValidator<CreateProductDto> _createValidator;
    private readonly IValidator<UpdatePriceDto> _updatePriceValidator;
    private readonly IValidator<RemoveStockDto> _removeStockValidator;
    private readonly IValidator<AddStockDto> _addStockValidator;

    public ProductsController(
        IProductService productService,
        IValidator<CreateProductDto> createValidator,
        IValidator<UpdatePriceDto> updatePriceValidator,
        IValidator<RemoveStockDto> removeStockValidator,
        IValidator<AddStockDto> addStockValidator)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updatePriceValidator = updatePriceValidator ?? throw new ArgumentNullException(nameof(updatePriceValidator));
        _removeStockValidator = removeStockValidator ?? throw new ArgumentNullException(nameof(removeStockValidator));
        _addStockValidator = addStockValidator ?? throw new ArgumentNullException(nameof(addStockValidator));
    }

    /// <summary>
    /// Отримати список позицій каталогу з пагінацією, фільтрацією та сортуванням на рівні SQL
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ProductDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<PagedResult<ProductDto>>> GetPaged([FromQuery] ProductQuery query, CancellationToken ct)
    {
        if (query.Page < 1)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Некоректний номер сторінки",
                Detail = "Номер сторінки (Page) повинен бути більшим або рівним 1."
            });
        }

        var result = await _productService.GetPagedAsync(query, ct);

        // Додавання метаданих пагінації у заголовок X-Pagination
        var metadata = new
        {
            result.TotalCount,
            result.Page,
            result.PageSize,
            result.TotalPages,
            result.HasNext,
            result.HasPrevious
        };
        Response.Headers["X-Pagination"] = JsonSerializer.Serialize(metadata);

        return Ok(result);
    }

    /// <summary>
    /// Отримати детальну інформацію про позицію каталогу за ID (демонстрація Eager Loading)
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(long id, CancellationToken ct)
    {
        var product = await _productService.GetByIdAsync(id, ct);
        return Ok(product);
    }

    /// <summary>
    /// Створити нову позицію каталогу
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<ProductDto>> Create(
        [FromBody] CreateProductDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        // Явний виклик валідатора FluentValidation
        var validationResult = await _createValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Помилка валідації вхідних даних",
                Detail = "Одне або декілька полів не пройшли валідацію."
            });
        }

        var created = await _productService.CreateAsync(dto, user ?? "system", ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    /// <summary>
    /// Змінити ціну позиції каталогу з фіксацією в історії тарифів
    /// </summary>
    [HttpPut("{id:long}/price")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdatePrice(
        long id,
        [FromBody] UpdatePriceDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var validationResult = await _updatePriceValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary())
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Помилка валідації ціни"
            });
        }

        await _productService.UpdatePriceAsync(id, dto, user ?? "system", ct);
        return NoContent();
    }

    /// <summary>
    /// Списати залишок товару на складі
    /// </summary>
    [HttpPost("{id:long}/stock/remove")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> RemoveStock(long id, [FromBody] RemoveStockDto dto, CancellationToken ct)
    {
        var validationResult = await _removeStockValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        await _productService.RemoveStockAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>
    /// Поповнити залишок товару на складі
    /// </summary>
    [HttpPost("{id:long}/stock/add")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> AddStock(long id, [FromBody] AddStockDto dto, CancellationToken ct)
    {
        var validationResult = await _addStockValidator.ValidateAsync(dto, ct);
        if (!validationResult.IsValid)
        {
            return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
        }

        await _productService.AddStockAsync(id, dto, ct);
        return NoContent();
    }

    /// <summary>
    /// Видалити позицію каталогу (Soft Delete)
    /// </summary>
    [HttpDelete("{id:long}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        long id,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        await _productService.DeleteAsync(id, user ?? "system", ct);
        return NoContent();
    }

    /// <summary>
    /// Отримати товари певної категорії за допомогою специфікації ProductsByCategorySpec
    /// </summary>
    [HttpGet("by-category/{categoryId:long}")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetByCategory(long categoryId, CancellationToken ct)
    {
        var products = await _productService.GetByCategoryAsync(categoryId, ct);
        return Ok(products);
    }
}
