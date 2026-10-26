namespace Catalog.Domain.Exceptions;

public class EntityNotFoundException : DomainException
{
    public EntityNotFoundException(string entityName, object key)
        : base($"Сутність '{entityName}' з ідентифікатором '{key}' не знайдена.")
    {
    }

    public EntityNotFoundException(string message)
        : base(message)
    {
    }
}
