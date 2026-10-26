using Activities.Domain.Common;
using Activities.Domain.Exceptions;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Незмінний Value Object пов'язаної послуги або цифрового товару з каталогу
/// </summary>
public sealed class RelatedProduct : ValueObject
{
    public long ProductId { get; }
    public string Name { get; }

    public RelatedProduct(long productId, string name)
    {
        if (productId <= 0)
        {
            throw new ValidationException("Ідентифікатор товару (ProductId) повинен бути більшим за 0.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ValidationException("Назва товару не може бути порожньою.");
        }

        if (name.Trim().Length > 200)
        {
            throw new ValidationException("Назва товару не може перевищувати 200 символів.");
        }

        ProductId = productId;
        Name = name.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return ProductId;
        yield return Name;
    }

    public override string ToString() => $"{Name} (ID: {ProductId})";
}
