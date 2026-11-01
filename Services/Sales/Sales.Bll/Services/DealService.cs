using System.Data;
using System.Diagnostics;
using AutoMapper;
using Microsoft.Extensions.Logging;
using Sales.Bll.Dto;
using Sales.Bll.Validation;
using Sales.Dal.Interfaces;
using Sales.Domain.Constants;
using Sales.Domain.Exceptions;
using Sales.Domain.Models;

namespace Sales.Bll.Services;

public interface IDealService
{
    Task<DealDto?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PagedResult<DealSummaryDto>> GetPageAsync(DealQueryDto query, CancellationToken ct = default);
    Task<DealDto> CreateAsync(CreateDealDto dto, string createdBy, CancellationToken ct = default);
    Task<bool> AddItemAsync(long dealId, AddDealItemDto dto, string addedBy, CancellationToken ct = default);
    Task<bool> AdvanceAsync(long dealId, string changedBy, string? reason, CancellationToken ct = default);
    Task<bool> WinDealAsync(long dealId, string performedBy, CancellationToken ct = default);
    Task<bool> LoseAsync(long dealId, LoseDealDto dto, string changedBy, CancellationToken ct = default);
    Task<bool> SoftDeleteAsync(long id, string deletedBy, CancellationToken ct = default);
    Task<IReadOnlyList<DealStatsDto>> GetStatsAsync(CancellationToken ct = default);
}

public class DealService : IDealService
{
    private static readonly ActivitySource Source = new("Sales.Api");

    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IDealValidator _validator;
    private readonly ILogger<DealService>? _logger;

    public DealService(
        IUnitOfWork uow,
        IMapper mapper,
        IDealValidator validator,
        ILogger<DealService>? logger = null)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
        _logger = logger;
    }

    public async Task<DealDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("GetDealById");
        activity?.SetTag("order.id", id);
        activity?.SetTag("deal.id", id);

        var deal = await _uow.Deals.GetWithDetailsAsync(id, ct);
        if (deal == null || deal.IsDeleted)
        {
            return null;
        }

        return _mapper.Map<DealDto>(deal);
    }

    public async Task<PagedResult<DealSummaryDto>> GetPageAsync(DealQueryDto query, CancellationToken ct = default)
    {
        var (items, total) = await _uow.Deals.GetPageAsync(
            query.Status,
            query.OwnerId,
            query.ClientId,
            query.Page,
            query.PageSize,
            ct);

        var dtos = _mapper.Map<IReadOnlyList<DealSummaryDto>>(items);
        return new PagedResult<DealSummaryDto>(dtos, total, query.Page, query.PageSize);
    }

    public async Task<DealDto> CreateAsync(CreateDealDto dto, string createdBy, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("CreateDeal");
        activity?.SetTag("deal.clientId", dto.ClientId);
        activity?.SetTag("deal.ownerId", dto.OwnerId);
        activity?.SetTag("deal.itemsCount", dto.Items.Count);

        // Рівень 2: Бізнес-валідація перед транзакцією
        await _validator.ValidateCreateAsync(dto, ct);

        // Виконання створення агрегату в межах UoW-транзакції
        await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            var deal = new Deal
            {
                Title = dto.Title,
                ClientId = dto.ClientId,
                OwnerId = dto.OwnerId,
                CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy
            };

            var details = new DealDetails
            {
                Source = dto.Source ?? "Inbound",
                Description = dto.Description,
                ExpectedCloseDate = dto.ExpectedCloseDate
            };

            var items = new List<DealItem>();
            foreach (var itemDto in dto.Items)
            {
                // Читання товару/послуги з локальної копії каталогу
                var product = await _uow.Products.GetByIdAsync(itemDto.ProductId, ct);
                if (product == null || !product.IsActive)
                {
                    throw new BusinessConflictException(
                        $"Product with ID {itemDto.ProductId} is not found or inactive in catalog copy.");
                }

                if (itemDto.Discount > (product.Price * itemDto.Quantity))
                {
                    throw new ValidationException(
                        $"Знижка {itemDto.Discount} грн перевищує повну вартість позиції {product.Price * itemDto.Quantity} грн для послуги '{product.Name}'.");
                }

                // знімок на момент додавання, політика дублювання Лаб. 01, не оновлюється
                items.Add(new DealItem
                {
                    ProductId = product.Id,
                    ProductName = product.Name,
                    UnitPrice = product.Price,
                    Quantity = itemDto.Quantity,
                    Discount = itemDto.Discount,
                    CreatedBy = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy
                });
            }

            var newDealId = await _uow.Deals.CreateAsync(deal, details, items, ct);
            var created = await _uow.Deals.GetWithDetailsAsync(newDealId, ct);
            if (created == null)
            {
                throw new InvalidOperationException("Failed to retrieve created deal.");
            }

            await _uow.CommitAsync(ct);

            activity?.SetTag("order.id", newDealId);
            activity?.SetTag("deal.id", newDealId);
            activity?.SetTag("deal.status", created.Stage);

            _logger?.LogInformation(
                "Order {OrderId} (Deal {DealId}) created for Client {ClientId}, {ItemsCount} items, Amount {Amount}, status {Status}",
                newDealId,
                newDealId,
                created.ClientId,
                items.Count,
                created.TotalAmount,
                created.Stage);

            return _mapper.Map<DealDto>(created);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to create deal for Client {ClientId}, rolling back transaction", dto.ClientId);
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> AddItemAsync(long dealId, AddDealItemDto dto, string addedBy, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("AddDealItem");
        activity?.SetTag("order.id", dealId);
        activity?.SetTag("product.id", dto.ProductId);

        var deal = await _uow.Deals.GetWithDetailsAsync(dealId, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", dealId);
        }

        if (DealStatuses.IsClosed(deal.Stage))
        {
            throw new BusinessConflictException($"Cannot add item to closed deal in stage '{deal.Stage}'.");
        }

        // Перевірка та знімок товару з репліки Каталогу
        var product = await _uow.Products.GetByIdAsync(dto.ProductId, ct);
        if (product == null || !product.IsActive)
        {
            throw new BusinessConflictException(
                $"Product with ID '{dto.ProductId}' not found or inactive in catalog copy.");
        }

        if (dto.Discount > (product.Price * dto.Quantity))
        {
            throw new ValidationException(
                $"Знижка {dto.Discount} грн перевищує повну вартість позиції {product.Price * dto.Quantity} грн для послуги '{product.Name}'.");
        }

        // знімок на момент додавання, політика дублювання Лаб. 01, не оновлюється
        await _validator.ValidateAddItemAsync(dealId, dto, ct);

        var added = await _uow.Deals.AddItemAsync(
            dealId,
            dto.ProductId,
            dto.Quantity,
            dto.Discount,
            string.IsNullOrWhiteSpace(addedBy) ? "system" : addedBy,
            ct);

        _logger?.LogInformation(
            "Added item Product {ProductId} (Qty: {Quantity}) to Order {OrderId}, status {Status}",
            dto.ProductId,
            dto.Quantity,
            dealId,
            deal.Stage);

        return added;
    }

    public async Task<bool> AdvanceAsync(long dealId, string changedBy, string? reason, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("AdvanceDealStage");
        activity?.SetTag("order.id", dealId);

        var deal = await _uow.Deals.GetWithDetailsAsync(dealId, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", dealId);
        }

        var result = await _uow.Deals.AdvanceAsync(
            dealId,
            string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy,
            reason,
            ct);

        _logger?.LogInformation(
            "Order {OrderId} advanced from stage {OldStatus} by {UserId}",
            dealId,
            deal.Stage,
            changedBy);

        return result;
    }

    /// <summary>
    /// ТРАНЗАКЦІЙНА БІЗНЕС-ОПЕРАЦІЯ через щонайменше 2 репозиторії:
    /// 1. Перевірка угоди та наявності позицій;
    /// 2. Deals.WinAsync (Dapper, збережена процедура usp_WinDeal);
    /// 3. Clients.RegisterWonDealAsync (чистий ADO.NET, перевіряє, що клієнт не видалений).
    /// Якщо клієнт м'яко видалений — Clients.RegisterWonDealAsync кидає BusinessConflictException,
    /// і зміни в Deals повністю відкочуються до вихідного стану (Negotiation)!
    /// </summary>
    public async Task<bool> WinDealAsync(long dealId, string performedBy, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("ConfirmOrder");
        activity?.SetTag("order.id", dealId);
        activity?.SetTag("deal.id", dealId);

        await _uow.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            // 1. Отримання угоди
            var deal = await _uow.Deals.GetWithDetailsAsync(dealId, ct);
            if (deal == null || deal.IsDeleted)
            {
                throw new NotFoundException("Deal", dealId);
            }

            if (deal.Items == null || deal.Items.Count == 0)
            {
                throw new BusinessConflictException("Неможливо виграти угоду без доданих товарів/послуг або якщо сума дорівнює нулю.");
            }

            var user = string.IsNullOrWhiteSpace(performedBy) ? "system" : performedBy;

            // 2. Перший репозиторій: Deals (Dapper / Stored Procedure)
            await _uow.Deals.WinAsync(dealId, user, "Успішне підписання договору з клієнтом", ct);

            // 3. Другий репозиторій: Clients (Чистий ADO.NET)
            // Якщо клієнт видалений — цей виклик кине BusinessConflictException!
            await _uow.Clients.RegisterWonDealAsync(deal.ClientId, user, ct);

            // 4. Фіксація транзакції
            await _uow.CommitAsync(ct);

            activity?.SetTag("order.status", "Won");
            _logger?.LogInformation(
                "Order {OrderId} confirmed, {ItemsCount} items, Amount {Amount}, status {Status} for Client {ClientId}",
                dealId,
                deal.Items.Count,
                deal.TotalAmount,
                "Won",
                deal.ClientId);

            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Rolling back WinDeal transaction for deal {DealId}", dealId);
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> LoseAsync(long dealId, LoseDealDto dto, string changedBy, CancellationToken ct = default)
    {
        using var activity = Source.StartActivity("LoseDeal");
        activity?.SetTag("order.id", dealId);

        var deal = await _uow.Deals.GetWithDetailsAsync(dealId, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", dealId);
        }

        if (DealStatuses.IsClosed(deal.Stage))
        {
            throw new BusinessConflictException($"Cannot mark as lost deal in closed stage '{deal.Stage}'.");
        }

        var lost = await _uow.Deals.LoseAsync(
            dealId,
            dto.Reason,
            string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy,
            ct);

        _logger?.LogInformation(
            "Order {OrderId} marked as {Status}, reason: {Reason}",
            dealId,
            "Lost",
            dto.Reason);

        return lost;
    }

    public async Task<bool> SoftDeleteAsync(long id, string deletedBy, CancellationToken ct = default)
    {
        var deal = await _uow.Deals.GetWithDetailsAsync(id, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", id);
        }

        // Перевірка бізнес-правила: виграну угоду не можна видалити
        _validator.ValidateDelete(deal);

        return await _uow.Deals.DeleteAsync(id, ct);
    }

    public async Task<IReadOnlyList<DealStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = await _uow.Deals.GetStatsByStatusAsync(ct);
        return _mapper.Map<IReadOnlyList<DealStatsDto>>(stats);
    }
}
