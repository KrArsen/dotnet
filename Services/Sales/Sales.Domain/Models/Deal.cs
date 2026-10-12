namespace Sales.Domain.Models;

/// <summary>
/// Корінь агрегату угоди (Deal Aggregate Root).
/// Інкапсулює деталі угоди (1:1), позиції зі знімками цін (1:N) та хронологію статусів.
/// </summary>
public class Deal
{
    public long Id { get; set; }
    public string DealNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public long ClientId { get; set; }
    public long OwnerId { get; set; }
    public string Stage { get; set; } = "Lead";
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public byte[]? RowVersion { get; set; }

    // Агрегатні зв'язки
    public DealDetails? Details { get; set; }
    public List<DealItem> Items { get; set; } = new();
    public List<DealStatusHistoryEntry> History { get; set; } = new();

    // Зручні навігаційні властивості для читання (опціонально)
    public Client? Client { get; set; }
    public TeamMember? Owner { get; set; }
}
