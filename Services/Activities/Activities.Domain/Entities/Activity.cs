using Activities.Domain.Common;
using Activities.Domain.Exceptions;
using Activities.Domain.ValueObjects;
using MongoDB.Bson;

namespace Activities.Domain.Entities;

/// <summary>
/// Агрегат активності/відгуку клієнтської комунікації CRM (MongoDB)
/// </summary>
public class Activity : BaseEntity
{
    private static readonly HashSet<string> AllowedTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "call", "email", "meeting", "note", "task"
    };

    public string Type { get; private set; } = string.Empty;
    public long ClientId { get; private set; }
    public string ClientName { get; private set; } = string.Empty;
    public long? DealId { get; private set; }
    public string? DealTitle { get; private set; }
    public Author Author { get; private set; } = null!;
    public string Text { get; private set; } = string.Empty;
    public Rating? Rating { get; private set; }
    public ObjectId? DiscussionId { get; private set; }

    // Поля для type = "call"
    public string? Direction { get; private set; }
    public int? DurationSec { get; private set; }
    public string? Outcome { get; private set; }

    // Поля для type = "email"
    public string? Subject { get; private set; }
    private readonly List<string> _attachments = new();
    public IReadOnlyList<string> Attachments => _attachments.AsReadOnly();

    // Поля для type = "meeting"
    public string? Location { get; private set; }
    private readonly List<string> _participants = new();
    public IReadOnlyList<string> Participants => _participants.AsReadOnly();
    public string? Agenda { get; private set; }

    // Поля для type = "task"
    public DateTime? DueDate { get; private set; }
    public bool? Done { get; private set; }

    // Вбудовані колекції
    private readonly List<Comment> _comments = new();
    public IReadOnlyCollection<Comment> Comments => _comments.AsReadOnly();

    private readonly List<RelatedProduct> _relatedProducts = new();
    public IReadOnlyCollection<RelatedProduct> RelatedProducts => _relatedProducts.AsReadOnly();

    // Приватний конструктор без параметрів для MongoDB BSON десеріалізації
    protected Activity()
    {
    }

    public Activity(
        string type,
        long clientId,
        string clientName,
        Author author,
        string text,
        Rating? rating = null,
        long? dealId = null,
        string? dealTitle = null,
        ObjectId? discussionId = null,
        string? direction = null,
        int? durationSec = null,
        string? outcome = null,
        string? subject = null,
        IEnumerable<string>? attachments = null,
        string? location = null,
        IEnumerable<string>? participants = null,
        string? agenda = null,
        DateTime? dueDate = null,
        bool? done = null,
        IEnumerable<RelatedProduct>? relatedProducts = null)
    {
        ValidateType(type);

        if (clientId <= 0)
        {
            throw new ValidationException("Ідентифікатор клієнта (ClientId) повинен бути більшим за 0.");
        }

        if (string.IsNullOrWhiteSpace(clientName))
        {
            throw new ValidationException("Назва клієнта (ClientName) є обов'язковою.");
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Текст активності/відгуку є обов'язковим.");
        }

        if (text.Trim().Length > 5000)
        {
            throw new ValidationException("Текст активності/відгуку не може перевищувати 5000 символів.");
        }

        Type = type.ToLowerInvariant().Trim();
        ClientId = clientId;
        ClientName = clientName.Trim();
        Author = author ?? throw new ArgumentNullException(nameof(author));
        Text = text.Trim();
        Rating = rating;
        DealId = dealId;
        DealTitle = dealTitle?.Trim();
        DiscussionId = discussionId;

        Direction = direction;
        DurationSec = durationSec;
        Outcome = outcome;
        Subject = subject;
        if (attachments != null) _attachments.AddRange(attachments);
        Location = location;
        if (participants != null) _participants.AddRange(participants);
        Agenda = agenda;
        DueDate = dueDate;
        Done = done;

        if (relatedProducts != null)
        {
            _relatedProducts.AddRange(relatedProducts);
        }
    }

    /// <summary>
    /// Оновлення тексту активності та рейтингу зі зміною версії
    /// </summary>
    public void Update(string text, Rating? rating = null, string? outcome = null)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ValidationException("Текст не може бути порожнім.");
        }

        if (text.Trim().Length > 5000)
        {
            throw new ValidationException("Текст не може перевищувати 5000 символів.");
        }

        Text = text.Trim();
        if (rating != null)
        {
            Rating = rating;
        }

        if (outcome != null)
        {
            Outcome = outcome;
        }

        BumpVersion();
    }

    /// <summary>
    /// Додавання вбудованого коментаря команди
    /// </summary>
    public void AddComment(string author, string text)
    {
        var comment = new Comment(author, text);
        _comments.Add(comment);
        BumpVersion();
    }

    /// <summary>
    /// Додавання денормалізованого товару/послуги з каталогу
    /// </summary>
    public void AddRelatedProduct(long productId, string name)
    {
        var product = new RelatedProduct(productId, name);
        if (!_relatedProducts.Any(p => p.ProductId == productId))
        {
            _relatedProducts.Add(product);
            Touch();
        }
    }

    /// <summary>
    /// Позначення завдання як виконаного
    /// </summary>
    public void MarkTaskCompleted()
    {
        if (!string.Equals(Type, "task", StringComparison.OrdinalIgnoreCase))
        {
            throw new DomainException("Лише активність типу 'task' може бути позначена як виконана.");
        }

        Done = true;
        BumpVersion();
    }

    private static void ValidateType(string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new ValidationException("Тип активності є обов'язковим.");
        }

        var normalized = type.Trim().ToLowerInvariant();
        if (normalized == "fax")
        {
            throw new DomainException("Тип активності 'fax' суворо заборонено схемою предметної області!");
        }

        if (!AllowedTypes.Contains(normalized))
        {
            throw new DomainException($"Невідомий тип активності '{type}'. Дозволені типи: call, email, meeting, note, task.");
        }
    }
}
