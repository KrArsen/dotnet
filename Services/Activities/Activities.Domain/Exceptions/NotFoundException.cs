namespace Activities.Domain.Exceptions;

/// <summary>
/// Виняток відсутності запитуваного ресурсу
/// </summary>
public class NotFoundException : DomainException
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string entityName, object key)
        : base($"Сутність '{entityName}' з ідентифікатором '{key}' не знайдена.")
    {
    }
}
