using System.Data;
using AutoMapper;
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
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IDealValidator _validator;

    public DealService(IUnitOfWork uow, IMapper mapper, IDealValidator validator)
    {
        _uow = uow ?? throw new ArgumentNullException(nameof(uow));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _validator = validator ?? throw new ArgumentNullException(nameof(validator));
    }

    public async Task<DealDto?> GetByIdAsync(long id, CancellationToken ct = default)
    {
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
        // Рівень 2: Бізнес-валідація
        await _validator.ValidateCreateAsync(dto, ct);

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

        var items = dto.Items.Select(i => new DealItem
        {
            ProductId = i.ProductId,
            Quantity = i.Quantity,
            Discount = i.Discount
        }).ToList();

        var newDealId = await _uow.Deals.CreateAsync(deal, details, items, ct);

        var created = await _uow.Deals.GetWithDetailsAsync(newDealId, ct);
        if (created == null)
        {
            throw new InvalidOperationException("Failed to retrieve created deal.");
        }

        return _mapper.Map<DealDto>(created);
    }

    public async Task<bool> AddItemAsync(long dealId, AddDealItemDto dto, string addedBy, CancellationToken ct = default)
    {
        var deal = await _uow.Deals.GetWithDetailsAsync(dealId, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", dealId);
        }

        if (DealStatuses.IsClosed(deal.Stage))
        {
            throw new BusinessConflictException($"Cannot add item to closed deal in stage '{deal.Stage}'.");
        }

        await _validator.ValidateAddItemAsync(dealId, dto, ct);

        return await _uow.Deals.AddItemAsync(
            dealId,
            dto.ProductId,
            dto.Quantity,
            dto.Discount,
            string.IsNullOrWhiteSpace(addedBy) ? "system" : addedBy,
            ct);
    }

    public async Task<bool> AdvanceAsync(long dealId, string changedBy, string? reason, CancellationToken ct = default)
    {
        var deal = await _uow.Deals.GetWithDetailsAsync(dealId, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", dealId);
        }

        return await _uow.Deals.AdvanceAsync(
            dealId,
            string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy,
            reason,
            ct);
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
            return true;
        }
        catch
        {
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<bool> LoseAsync(long dealId, LoseDealDto dto, string changedBy, CancellationToken ct = default)
    {
        var deal = await _uow.Deals.GetWithDetailsAsync(dealId, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", dealId);
        }

        return await _uow.Deals.LoseAsync(
            dealId,
            dto.Reason,
            string.IsNullOrWhiteSpace(changedBy) ? "system" : changedBy,
            ct);
    }

    public async Task<bool> SoftDeleteAsync(long id, string deletedBy, CancellationToken ct = default)
    {
        var deal = await _uow.Deals.GetWithDetailsAsync(id, ct);
        if (deal == null || deal.IsDeleted)
        {
            throw new NotFoundException("Deal", id);
        }

        _validator.ValidateDelete(deal);

        return await _uow.Deals.DeleteAsync(id, ct);
    }

    public async Task<IReadOnlyList<DealStatsDto>> GetStatsAsync(CancellationToken ct = default)
    {
        var stats = await _uow.Deals.GetStatsByStatusAsync(ct);
        return _mapper.Map<IReadOnlyList<DealStatsDto>>(stats);
    }
}
