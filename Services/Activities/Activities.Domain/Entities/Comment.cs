using Activities.Domain.Exceptions;

namespace Activities.Domain.Entities;

/// <summary>
/// Вкладений коментар співробітника до активності/відгуку
/// </summary>
public class Comment
{
    public string Author { get; private set; } = string.Empty;
    public string Text { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    // Приватний конструктор для MongoDB десеріалізації
    private Comment()
    {
    }

    public Comment(string author, string text, DateTime? createdAt = null)
    {
        if (string.IsNullOrWhiteSpace(author))
        {
            throw new ValidationException("Автор коментаря не може бути порожнім.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Текст коментаря не може бути порожнім.");
        }

        if (text.Trim().Length > 2000)
        {
            throw new ValidationException("Текст коментаря не може перевищувати 2000 символів.");
        }

        Author = author.Trim();
        Text = text.Trim();
        CreatedAt = createdAt ?? DateTime.UtcNow;
    }
}
