namespace Sales.Domain.Models;

/// <summary>
/// Локальна репліка товару/послуги з контексту Каталогу (CatalogDb).
/// У контексті Продажів доступна лише для читання; оновлюється через події від Каталогу.
/// </summary>
public class Product
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
}
