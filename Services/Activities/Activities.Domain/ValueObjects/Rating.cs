using Activities.Domain.Common;
using Activities.Domain.Exceptions;

namespace Activities.Domain.ValueObjects;

/// <summary>
/// Незмінний Value Object числової оцінки (1..5)
/// </summary>
public sealed class Rating : ValueObject
{
    public int Value { get; }

    public Rating(int value)
    {
        if (value is < 1 or > 5)
        {
            throw new DomainException($"Некоректний рейтинг '{value}'. Рейтинг має становити від 1 до 5.");
        }

        Value = value;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public static implicit operator int(Rating rating) => rating.Value;
    public static explicit operator Rating(int value) => new(value);

    public override string ToString() => Value.ToString();
}
