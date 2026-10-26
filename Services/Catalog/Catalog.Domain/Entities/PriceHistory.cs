using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class PriceHistory
{
    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public decimal Price { get; private set; }
    public DateTime ValidFrom { get; private set; } = DateTime.UtcNow;
    public DateTime? ValidTo { get; private set; }
    public string ChangedBy { get; private set; } = "system";
    public string? Reason { get; private set; }

    // Навігаційна властивість
    public Product Product { get; private set; } = null!;

    private PriceHistory()
    {
    }

    public PriceHistory(Product product, decimal price, DateTime validFrom, string changedBy = "system", string? reason = null)
    {
        if (price <= 0)
            throw new DomainException("Історична ціна має бути більшою за 0.");

        Product = product ?? throw new ArgumentNullException(nameof(product));
        ProductId = product.Id;
        Price = price;
        ValidFrom = validFrom;
        ChangedBy = changedBy;
        Reason = reason;
    }

    public PriceHistory(long productId, decimal price, DateTime validFrom, DateTime? validTo = null, string changedBy = "system", string? reason = null)
    {
        if (price <= 0)
            throw new DomainException("Історична ціна має бути більшою за 0.");

        ProductId = productId;
        Price = price;
        ValidFrom = validFrom;
        ValidTo = validTo;
        ChangedBy = changedBy;
        Reason = reason;
    }

    public void Close(DateTime validTo)
    {
        if (validTo < ValidFrom)
            throw new DomainException("Дата закінчення дії тарифу не може бути ранішою за дату початку.");

        ValidTo = validTo;
    }
}
