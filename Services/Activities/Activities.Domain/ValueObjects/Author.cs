using Activities.Domain.Common;
using Activities.Domain.Exceptions;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Незмінний Value Object автора комунікації або відгуку
/// </summary>
public sealed class Author : ValueObject
{
    public long UserId { get; }
    public string DisplayName { get; }

    public Author(long userId, string displayName)
    {
        if (userId <= 0)
        {
            throw new ValidationException("Ідентифікатор автора (UserId) повинен бути більшим за 0.");
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ValidationException("Ім'я автора (DisplayName) не може бути порожнім.");
        }

        if (displayName.Trim().Length > 100)
        {
            throw new ValidationException("Ім'я автора не може перевищувати 100 символів.");
        }

        UserId = userId;
        DisplayName = displayName.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UserId;
        yield return DisplayName;
    }

    public override string ToString() => $"{DisplayName} (#{UserId})";
}
