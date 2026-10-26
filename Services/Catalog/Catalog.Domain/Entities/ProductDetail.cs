using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class ProductDetail
{
    // PK і водночас FK до Product
    public long ProductId { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public string Unit { get; private set; } = "послуга";
    public int? DurationMinutes { get; private set; }
    public decimal TaxRate { get; private set; } = 20.00m;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string CreatedBy { get; private set; } = "system";
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }

    // Навігаційна властивість 1:1
    public Product Product { get; private set; } = null!;

    private ProductDetail()
    {
    }

    public ProductDetail(
        Product product,
        string description,
        string unit = "послуга",
        int? durationMinutes = null,
        decimal taxRate = 20.00m,
        string createdBy = "system")
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        ProductId = product.Id;
        Update(description, unit, durationMinutes, taxRate, createdBy);
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    // Додатковий конструктор для сідінгу за ProductId
    public ProductDetail(
        long productId,
        string description,
        string unit = "послуга",
        int? durationMinutes = null,
        decimal taxRate = 20.00m,
        string createdBy = "system")
    {
        ProductId = productId;
        Update(description, unit, durationMinutes, taxRate, createdBy);
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
    }

    public void Update(
        string description,
        string unit = "послуга",
        int? durationMinutes = null,
        decimal taxRate = 20.00m,
        string updatedBy = "system")
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new DomainException("Опис деталізації послуги/товару не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(unit))
            throw new DomainException("Одиниця виміру не може бути порожньою.");

        if (taxRate < 0)
            throw new DomainException("Ставка податку не може бути від'ємною.");

        if (durationMinutes.HasValue && durationMinutes.Value < 0)
            throw new DomainException("Тривалість у хвилинах не може бути від'ємною.");

        Description = description.Trim();
        Unit = unit.Trim();
        DurationMinutes = durationMinutes;
        TaxRate = taxRate;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }
}
