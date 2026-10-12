namespace Sales.Domain.Models;

/// <summary>
/// Позиція угоди. Містить незмінні знімки (snapshots) назви та ціни товару/послуги
/// на момент додавання до комерційної пропозиції/угоди.
/// </summary>
public class DealItem
{
    public long DealId { get; set; }
    public long ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }
    public decimal Discount { get; set; }
    public int Quantity { get; set; } = 1;
    public decimal TotalPrice => (Quantity * UnitPrice) - Discount;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}
