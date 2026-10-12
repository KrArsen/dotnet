using System.ComponentModel.DataAnnotations;

namespace Sales.Bll.Dto;

public class CreateDealItemDto
{
    [Required(ErrorMessage = "ProductId є обов'язковим.")]
    [Range(1, long.MaxValue, ErrorMessage = "ProductId має бути додатним числом.")]
    public long ProductId { get; set; }

    [Range(1, 10000, ErrorMessage = "Кількість має бути від 1 до 10000.")]
    public int Quantity { get; set; } = 1;

    [Range(0, 10000000, ErrorMessage = "Знижка не може бути від'ємною.")]
    public decimal Discount { get; set; } = 0.00m;
}

public class CreateDealDto
{
    [Required(ErrorMessage = "ClientId є обов'язковим.")]
    [Range(1, long.MaxValue, ErrorMessage = "ClientId має бути додатним числом.")]
    public long ClientId { get; set; }

    [Required(ErrorMessage = "OwnerId є обов'язковим.")]
    [Range(1, long.MaxValue, ErrorMessage = "OwnerId має бути додатним числом.")]
    public long OwnerId { get; set; }

    [Required(ErrorMessage = "Назва угоди є обов'язковою.")]
    [StringLength(250, MinimumLength = 3, ErrorMessage = "Назва угоди має містити від 3 до 250 символів.")]
    public string Title { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "Джерело ліда не повинно перевищувати 100 символів.")]
    public string? Source { get; set; } = "Inbound";

    [StringLength(4000, ErrorMessage = "Опис не повинен перевищувати 4000 символів.")]
    public string? Description { get; set; }

    public DateTime? ExpectedCloseDate { get; set; }

    [Required(ErrorMessage = "Список позицій не може бути порожнім.")]
    [MinLength(1, ErrorMessage = "Угода має містити щонайменше 1 позицію.")]
    public List<CreateDealItemDto> Items { get; set; } = new();
}

public class AddDealItemDto
{
    [Required(ErrorMessage = "ProductId є обов'язковим.")]
    [Range(1, long.MaxValue, ErrorMessage = "ProductId має бути додатним числом.")]
    public long ProductId { get; set; }

    [Range(1, 10000, ErrorMessage = "Кількість має бути від 1 до 10000.")]
    public int Quantity { get; set; } = 1;

    [Range(0, 10000000, ErrorMessage = "Знижка не може бути від'ємною.")]
    public decimal Discount { get; set; } = 0.00m;
}

public class AdvanceDealDto
{
    [StringLength(500, ErrorMessage = "Причина/коментар не повинен перевищувати 500 символів.")]
    public string? Reason { get; set; }
}

public class LoseDealDto
{
    [Required(ErrorMessage = "Причина зриву угоди (Reason) є обов'язковою.")]
    [StringLength(500, MinimumLength = 3, ErrorMessage = "Причина має містити від 3 до 500 символів.")]
    public string Reason { get; set; } = string.Empty;
}

public class DealQueryDto
{
    public string? Status { get; set; }
    public long? OwnerId { get; set; }
    public long? ClientId { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Номер сторінки має бути >= 1.")]
    public int Page { get; set; } = 1;

    [Range(1, 100, ErrorMessage = "Розмір сторінки має бути від 1 до 100.")]
    public int PageSize { get; set; } = 10;
}

public record DealDetailsDto(
    long DealId,
    string Source,
    string? Description,
    string? LostReason,
    DateTime? ExpectedCloseDate,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy);

public record DealItemDto(
    long DealId,
    long ProductId,
    string ProductName,
    decimal UnitPrice,
    decimal Discount,
    int Quantity,
    decimal TotalPrice,
    DateTime CreatedAt,
    string CreatedBy);

public record DealStatusHistoryDto(
    long Id,
    long DealId,
    string? OldStatus,
    string NewStatus,
    DateTime ChangedAt,
    string ChangedBy,
    string? Reason);

public record DealDto(
    long Id,
    string DealNumber,
    string Title,
    long ClientId,
    long OwnerId,
    string Stage,
    decimal TotalAmount,
    DateTime CreatedAt,
    string CreatedBy,
    DateTime? UpdatedAt,
    string? UpdatedBy,
    DealDetailsDto? Details,
    IReadOnlyList<DealItemDto> Items,
    IReadOnlyList<DealStatusHistoryDto> History,
    ClientDto? Client,
    TeamMemberDto? Owner);

public record DealSummaryDto(
    long DealId,
    string DealNumber,
    string Title,
    string Stage,
    decimal TotalAmount,
    long ClientId,
    string ClientCompanyName,
    string? ClientContactPerson,
    long OwnerId,
    string OwnerName,
    string? DealSource,
    DateTime? ExpectedCloseDate,
    string? LostReason,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    int ItemsCount,
    decimal CalculatedTotal);

public record DealStatsDto(
    string Stage,
    int DealsCount,
    decimal TotalAmount,
    decimal AverageAmount);
