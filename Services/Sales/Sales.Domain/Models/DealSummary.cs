namespace Sales.Domain.Models;

/// <summary>
/// Проєкція представлення vw_DealSummary для швидкої пагінації та фільтрації угод.
/// </summary>
public class DealSummary
{
    public long DealId { get; set; }
    public string DealNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public long ClientId { get; set; }
    public string ClientCompanyName { get; set; } = string.Empty;
    public string? ClientContactPerson { get; set; }
    public long OwnerId { get; set; }
    public string OwnerName { get; set; } = string.Empty;
    public string? DealSource { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public string? LostReason { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public int ItemsCount { get; set; }
    public decimal CalculatedTotal { get; set; }
}

public class DealStatusStat
{
    public string Stage { get; set; } = string.Empty;
    public int DealsCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal AverageAmount { get; set; }
}
