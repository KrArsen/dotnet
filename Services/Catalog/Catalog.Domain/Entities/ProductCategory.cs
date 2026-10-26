namespace Catalog.Domain.Entities;

public class ProductCategory
{
    // Складений первинний ключ: ProductId + CategoryId
    public long ProductId { get; private set; }
    public long CategoryId { get; private set; }

    // Власне поле проміжної сутності M:N
    public DateTime AddedAt { get; private set; } = DateTime.UtcNow;

    // Навігаційні властивості
    public Product Product { get; private set; } = null!;
    public Category Category { get; private set; } = null!;

    private ProductCategory()
    {
    }

    public ProductCategory(Product product, Category category)
    {
        Product = product ?? throw new ArgumentNullException(nameof(product));
        Category = category ?? throw new ArgumentNullException(nameof(category));
        ProductId = product.Id;
        CategoryId = category.Id;
        AddedAt = DateTime.UtcNow;
    }

    public ProductCategory(long productId, long categoryId, DateTime? addedAt = null)
    {
        ProductId = productId;
        CategoryId = categoryId;
        AddedAt = addedAt ?? DateTime.UtcNow;
    }
}
