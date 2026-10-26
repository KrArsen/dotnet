using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class Product
{
    public long Id { get; private set; }
    public string Sku { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string ItemType { get; private set; } = "Service";
    public decimal Price { get; private set; }
    public int StockQuantity { get; private set; }
    public bool IsActive { get; private set; } = true;
    public bool IsDeleted { get; private set; } = false;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public string CreatedBy { get; private set; } = "system";
    public DateTime? UpdatedAt { get; private set; }
    public string? UpdatedBy { get; private set; }
    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    // Навігаційні властивості
    public ProductDetail? Detail { get; private set; }
    public ICollection<ProductImage> Images { get; private set; } = new List<ProductImage>();
    public ICollection<ProductCategory> ProductCategories { get; private set; } = new List<ProductCategory>();
    public ICollection<PriceHistory> PriceHistories { get; private set; } = new List<PriceHistory>();

    // Приватний конструктор без параметрів для EF Core
    private Product()
    {
    }

    // Публічний конструктор з інкапсульованими бізнес-правилами
    public Product(
        string sku,
        string name,
        string itemType,
        decimal price,
        int stockQuantity = 100,
        string createdBy = "system")
    {
        if (string.IsNullOrWhiteSpace(sku))
            throw new DomainException("Артикул (SKU) позиції каталогу не може бути порожнім.");

        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Назва позиції каталогу не може бути порожньою.");

        if (price <= 0)
            throw new DomainException("Ціна позиції прайс-листа повинна бути більшою за 0.");

        if (stockQuantity < 0)
            throw new DomainException("Кількість на складі не може бути від'ємною.");

        if (itemType != "Service" && itemType != "Product")
            throw new DomainException("Тип елемента має бути 'Service' або 'Product'.");

        Sku = sku.Trim().ToUpperInvariant();
        Name = name.Trim();
        ItemType = itemType;
        Price = price;
        StockQuantity = stockQuantity;
        IsActive = true;
        IsDeleted = false;
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;

        // Фіксуємо початкову ціну в історії тарифів
        PriceHistories.Add(new PriceHistory(this, price, DateTime.UtcNow, createdBy, "Початкова фіксація вартості"));
    }

    // Інкапсульований метод зміни ціни
    public void ChangePrice(decimal newPrice, string changedBy = "system", string? reason = null)
    {
        if (newPrice <= 0)
            throw new DomainException("Нова ціна позиції каталогу має бути строго більшою за 0.");

        if (newPrice == Price)
            return;

        // Закриваємо попередній активний тариф в історії
        var activeHistory = PriceHistories.FirstOrDefault(h => h.ValidTo == null);
        activeHistory?.Close(DateTime.UtcNow);

        // Додаємо новий запис в історію тарифів
        PriceHistories.Add(new PriceHistory(this, newPrice, DateTime.UtcNow, changedBy, reason));

        Price = newPrice;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = changedBy;
    }

    // Інкапсульований метод списання залишку
    public void RemoveStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Кількість для списання повинна бути більшою за 0.");

        if (StockQuantity < quantity)
            throw new DomainException($"Недостатній залишок на складі для товару '{Name}'. Доступно: {StockQuantity}, запитано: {quantity}.");

        StockQuantity -= quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    // Інкапсульований метод поповнення залишку
    public void AddStock(int quantity)
    {
        if (quantity <= 0)
            throw new DomainException("Кількість для поповнення повинна бути більшою за 0.");

        StockQuantity += quantity;
        UpdatedAt = DateTime.UtcNow;
    }

    // Оновлення базових реквізитів
    public void UpdateInfo(string name, string itemType, bool isActive, string updatedBy = "system")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Назва позиції каталогу не може бути порожньою.");

        if (itemType != "Service" && itemType != "Product")
            throw new DomainException("Тип елемента має бути 'Service' або 'Product'.");

        Name = name.Trim();
        ItemType = itemType;
        IsActive = isActive;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    // Призначення деталізації (1:1)
    public void SetDetail(ProductDetail detail)
    {
        Detail = detail ?? throw new ArgumentNullException(nameof(detail));
    }

    // Додавання зображення (1:N)
    public void AddImage(string url, string? altText = null, bool isMain = false, int sortOrder = 0)
    {
        if (string.IsNullOrWhiteSpace(url))
            throw new DomainException("URL зображення не може бути порожнім.");

        if (isMain)
        {
            foreach (var img in Images)
                img.SetMain(false);
        }

        Images.Add(new ProductImage(this, url, altText, isMain, sortOrder));
    }

    // Призначення категорії (M:N)
    public void AssignCategory(Category category)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (!ProductCategories.Any(pc => pc.CategoryId == category.Id))
        {
            ProductCategories.Add(new ProductCategory(this, category));
        }
    }

    // М'яке видалення
    public void MarkDeleted(string deletedBy = "system")
    {
        IsDeleted = true;
        IsActive = false;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = deletedBy;
    }
}
