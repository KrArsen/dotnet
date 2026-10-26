namespace Catalog.Domain.Exceptions;

public class ValidationException : DomainException
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) : base(message)
    {
        Errors = new Dictionary<string, string[]>();
    }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("Виникла одна або декілька помилок валідації вхідних даних.")
    {
        Errors = errors;
    }
}
