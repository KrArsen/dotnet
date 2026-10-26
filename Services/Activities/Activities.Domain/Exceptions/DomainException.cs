namespace Activities.Domain.Exceptions;

/// <summary>
/// Базовий виняток порушення доменних інваріантів та бізнес-правил
/// </summary>
public class DomainException : Exception
{
    public DomainException(string message) : base(message)
    {
    }

    public DomainException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
