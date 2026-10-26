namespace Activities.Domain.Exceptions;

/// <summary>
/// Виняток валідації доменних параметрів конструктора або методу
/// </summary>
public class ValidationException : DomainException
{
    public ValidationException(string message) : base(message)
    {
    }
}
