namespace Activities.Domain.Exceptions;

/// <summary>
/// Виняток конфлікту версій або порушення унікальності стану (HTTP 409)
/// </summary>
public class ConflictException : DomainException
{
    public ConflictException(string message) : base(message)
    {
    }

    public ConflictException(string entityName, object key, int expectedVersion, int actualVersion)
        : base($"Конфлікт оптимістичного блокування для '{entityName}' (ID: '{key}'). Очікувана версія: {expectedVersion}, фактична в базі даних: {actualVersion}.")
    {
    }
}
