using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Catalog.Api.Metrics;
using Catalog.Bll.DTOs;
using Catalog.Bll.Services;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Catalog.Api.Controllers;

public sealed record UpdateProductRequest(string? Name = null, decimal? Price = null, string? Reason = null);

[ApiController]
[Authorize]
[Route("api/products")]
[Route("products")]
[Produces("application/json")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;
    private readonly IValidator<CreateProductDto> _createValidator;
    private readonly IValidator<UpdatePriceDto> _updatePriceValidator;
    private readonly IValidator<RemoveStockDto> _removeStockValidator;
    private readonly IValidator<AddStockDto> _addStockValidator;
    private readonly CatalogMetrics? _metrics;

    public ProductsController(
        IProductService productService,
        IValidator<CreateProductDto> createValidator,
        IValidator<UpdatePriceDto> updatePriceValidator,
        IValidator<RemoveStockDto> removeStockValidator,
        IValidator<AddStockDto> addStockValidator,
        CatalogMetrics? metrics = null)
    {
        _productService = productService ?? throw new ArgumentNullException(nameof(productService));
        _createValidator = createValidator ?? throw new ArgumentNullException(nameof(createValidator));
        _updatePriceValidator = updatePriceValidator ?? throw new ArgumentNullException(nameof(updatePriceValidator));
        _removeStockValidator = removeStockValidator ?? throw new ArgumentNullException(nameof(removeStockValidator));
        _addStockValidator = addStockValidator ?? throw new ArgumentNullException(nameof(addStockValidator));
        _metrics = metrics;
    }

    private string ResolveActor(string? headerUser) =>
        User.FindFirstValue("preferred_username")
        ?? User.FindFirstValue(JwtRegisteredClaimNames.UniqueName)
        ?? User.FindFirstValue(ClaimTypes.Name)
        ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? headerUser
        ?? "system";

    /// <summary>
    /// Отримати список позицій каталогу з пагінацією, фільтрацією та сортуванням на рівні SQL
    /// </summary>
    [HttpGet]
    [RequirePermission("products:read")]
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

        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
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

            _metrics?.RecordDuration("list", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(result);
        }
        catch
        {
            _metrics?.RecordDuration("list", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Отримати детальну інформацію про позицію каталогу за ID (демонстрація Eager Loading)
    /// </summary>
    [HttpGet("{id:long}")]
    [RequirePermission("products:read")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> GetById(long id, CancellationToken ct)
    {
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var product = await _productService.GetByIdAsync(id, ct);
            _metrics?.RecordDuration("get_by_id", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(product);
        }
        catch
        {
            _metrics?.RecordDuration("get_by_id", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Створити нову позицію каталогу (вимагає products:create — доступно лише admin)
    /// </summary>
    [HttpPost]
    [RequirePermission("products:create")]
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

        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            var created = await _productService.CreateAsync(dto, ResolveActor(user), ct);
            _metrics?.ProductCreated(created.ItemType ?? "Service");
            _metrics?.RecordDuration("create", success: true, sw.Elapsed.TotalMilliseconds);
            return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
        }
        catch
        {
            _metrics?.RecordDuration("create", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Оновити позицію каталогу (назву та/або ціну — вимагає products:update)
    /// </summary>
    [HttpPut("{id:long}")]
    [RequirePermission("products:update")]
    [ProducesResponseType(typeof(ProductDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ProductDto>> Update(
        long id,
        [FromBody] UpdateProductRequest request,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var actor = ResolveActor(user);
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            if (!string.IsNullOrWhiteSpace(request.Name))
            {
                await _productService.RenameAsync(id, new RenameProductDto(request.Name), actor, ct);
            }

            if (request.Price.HasValue)
            {
                var priceDto = new UpdatePriceDto(request.Price.Value, request.Reason ?? "Updated via PUT /products/{id}");
                var validationResult = await _updatePriceValidator.ValidateAsync(priceDto, ct);
                if (!validationResult.IsValid)
                {
                    return ValidationProblem(new ValidationProblemDetails(validationResult.ToDictionary()));
                }

                await _productService.UpdatePriceAsync(id, priceDto, actor, ct);
            }

            var updated = await _productService.GetByIdAsync(id, ct);
            _metrics?.ProductUpdated("product");
            _metrics?.RecordDuration("update", success: true, sw.Elapsed.TotalMilliseconds);
            return Ok(updated);
        }
        catch
        {
            _metrics?.RecordDuration("update", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Опублікувати позицію каталогу у вітрину (вимагає products:publish — admin / manager)
    /// </summary>
    [HttpPost("{id:long}/publish")]
    [RequirePermission("products:publish")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Publish(
        long id,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        var actor = ResolveActor(user);
        var product = await _productService.GetByIdAsync(id, ct);

        return Ok(new
        {
            product.Id,
            product.Sku,
            product.Name,
            published = true,
            publishedBy = actor,
            publishedAtUtc = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Змінити ціну позиції каталогу з фіксацією в історії тарифів
    /// </summary>
    [HttpPut("{id:long}/price")]
    [RequirePermission("products:update")]
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

        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            await _productService.UpdatePriceAsync(id, dto, ResolveActor(user), ct);
            _metrics?.ProductUpdated("price");
            _metrics?.RecordDuration("update_price", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("update_price", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Перейменувати позицію каталогу та опублікувати подію ProductRenamedEvent (оновлює Sales та Reviews)
    /// </summary>
    [HttpPut("{id:long}/name")]
    [RequirePermission("products:update")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rename(
        long id,
        [FromBody] RenameProductDto dto,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            await _productService.RenameAsync(id, dto, ResolveActor(user), ct);
            _metrics?.ProductUpdated("name");
            _metrics?.RecordDuration("rename", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("rename", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Списати залишок товару на складі
    /// </summary>
    [HttpPost("{id:long}/stock/remove")]
    [RequirePermission("products:update")]
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

        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            await _productService.RemoveStockAsync(id, dto, ct);
            _metrics?.ProductUpdated("stock_remove");
            _metrics?.RecordDuration("remove_stock", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("remove_stock", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Поповнити залишок товару на складі
    /// </summary>
    [HttpPost("{id:long}/stock/add")]
    [RequirePermission("products:update")]
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

        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            await _productService.AddStockAsync(id, dto, ct);
            _metrics?.ProductUpdated("stock_add");
            _metrics?.RecordDuration("add_stock", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("add_stock", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Видалити позицію каталогу (Soft Delete — вимагає products:delete, доступно лише admin)
    /// </summary>
    [HttpDelete("{id:long}")]
    [RequirePermission("products:delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        long id,
        [FromHeader(Name = "X-User")] string? user,
        CancellationToken ct)
    {
        _metrics?.RequestStarted();
        var sw = Stopwatch.StartNew();
        try
        {
            await _productService.DeleteAsync(id, ResolveActor(user), ct);
            _metrics?.ProductDeleted();
            _metrics?.RecordDuration("delete", success: true, sw.Elapsed.TotalMilliseconds);
            return NoContent();
        }
        catch
        {
            _metrics?.RecordDuration("delete", success: false, sw.Elapsed.TotalMilliseconds);
            throw;
        }
        finally
        {
            _metrics?.RequestCompleted();
        }
    }

    /// <summary>
    /// Отримати товари певної категорії за допомогою специфікації ProductsByCategorySpec
    /// </summary>
    [HttpGet("by-category/{categoryId:long}")]
    [RequirePermission("products:read")]
    [ProducesResponseType(typeof(IReadOnlyList<ProductDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ProductDto>>> GetByCategory(long categoryId, CancellationToken ct)
    {
        var products = await _productService.GetByCategoryAsync(categoryId, ct);
        return Ok(products);
    }
}
