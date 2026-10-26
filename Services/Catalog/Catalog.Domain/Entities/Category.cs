using Catalog.Domain.Exceptions;

namespace Catalog.Domain.Entities;

public class Category
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string CreatedBy { get; set; } = "system";
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; } = false;

    // Навігаційна властивість M:N
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();

    // Конструктор без параметрів для EF Core та HasData
    public Category()
    {
    }

    public Category(string name, string? description = null, string createdBy = "system")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Назва категорії не може бути порожньою.");

        Name = name.Trim();
        Description = description?.Trim();
        CreatedAt = DateTime.UtcNow;
        CreatedBy = createdBy;
        IsDeleted = false;
    }

    public void Update(string name, string? description, string updatedBy = "system")
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Назва категорії не може бути порожньою.");

        Name = name.Trim();
        Description = description?.Trim();
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = updatedBy;
    }

    public void MarkDeleted(string deletedBy = "system")
    {
        IsDeleted = true;
        UpdatedAt = DateTime.UtcNow;
        UpdatedBy = deletedBy;
    }
}
