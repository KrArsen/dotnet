using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class ProductImage
{
    public long Id { get; private set; }
    public long ProductId { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public string? AltText { get; private set; }
    public bool IsMain { get; private set; } = false;
    public int SortOrder { get; private set; } = 0;

    // Навігаційна властивість
    public Product Product { get; private set; } = null!;

    private ProductImage()
    {
    }

    public ProductImage(Product product, string url, string? altText = null, bool isMain = false, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("URL зображення не може бути порожнім.");

        Product = product ?? throw new ArgumentNullException(nameof(product));
        ProductId = product.Id;
        Url = url.Trim();
        AltText = altText?.Trim();
        IsMain = isMain;
        SortOrder = sortOrder;
    }

    public ProductImage(long productId, string url, string? altText = null, bool isMain = false, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("URL зображення не може бути порожнім.");

        ProductId = productId;
        Url = url.Trim();
        AltText = altText?.Trim();
        IsMain = isMain;
        SortOrder = sortOrder;
    }

    public void SetMain(bool isMain)
    {
        IsMain = isMain;
    }

    public void Update(string url, string? altText, int sortOrder)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("URL зображення не може бути порожнім.");

        Url = url.Trim();
        AltText = altText?.Trim();
        SortOrder = sortOrder;
    }
}
