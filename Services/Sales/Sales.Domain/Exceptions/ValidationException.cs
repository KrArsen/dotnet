namespace Sales.Domain.Exceptions;

public class ValidationException : Exception
{
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(string message) 
        : base(message)
    {
        Errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    public ValidationException(string message, IDictionary<string, string[]> errors) 
        : base(message)
    {
        Errors = errors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }

    public ValidationException(IDictionary<string, string[]> errors) 
        : base("One or more validation errors occurred.")
    {
        Errors = errors ?? new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
    }
}
