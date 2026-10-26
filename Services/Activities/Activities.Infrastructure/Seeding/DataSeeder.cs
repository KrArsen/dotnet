using Activities.Domain.Common;
using Activities.Domain.Entities;
using Activities.Domain.ValueObjects;
using Activities.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Activities.Infrastructure.Seeding;

/// <summary>
/// Ідемпотентний наповнювач початкових даних MongoDB на основі db/p3/seed.js
/// </summary>
public class DataSeeder : IDataSeeder
{
    private readonly MongoDbContext _context;
    private readonly ILogger<DataSeeder> _logger;

    public DataSeeder(MongoDbContext context, ILogger<DataSeeder> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var count = await _context.Activities.EstimatedDocumentCountAsync(cancellationToken: cancellationToken);
            if (count > 0)
            {
                _logger.LogInformation("MongoDB 'activities' collection already contains {Count} documents. Skipping seed.", count);
                return;
            }

            _logger.LogInformation("Seeding initial activities into MongoDB based on db/p3/seed.js...");

            var author1 = new Author(1, "Андрій Мельник");
            var author2 = new Author(2, "Вікторія Коваль");
            var author3 = new Author(3, "Сергій Бондаренко");
            var author4 = new Author(4, "Марина Савченко");

            var disc1Id = new ObjectId("651000000000000000000001");
            var disc2Id = new ObjectId("651000000000000000000002");

            var items = new List<Activity>
            {
                // 1. Дзвінок вхідний (АгроТрейд Захід)
                CreateActivity(
                    "653000000000000000000001", "call", 1, "ТОВ «АгроТрейд Захід»", author2,
                    "Первинний дзвінок: обговорили редизайн старого сайту, інтеграцію з каталогом зернових культур.",
                    new Rating(5), dealId: 1, dealTitle: "Корпоративний портал для АгроТрейд Захід",
                    direction: "inbound", durationSec: 420, outcome: "Клієнт зацікавлений у розробці сайту на Vue.js та надійному хостингу",
                    discussionId: disc1Id,
                    comments: new[] { new Comment("Андрій Мельник", "Чудовий контакт, підготуй кошторис на понеділок.") },
                    relatedProducts: new[] { new RelatedProduct(2, "Корпоративний сайт") }),

                // 2. Дзвінок вихідний (Сидоренко та партнери)
                CreateActivity(
                    "653000000000000000000002", "call", 2, "Адвокатське об'єднання «Сидоренко та партнери»", author2,
                    "Уточнення вимог до лендінгу та корпоративного логотипу.",
                    new Rating(4), dealId: 2, dealTitle: "Розробка брендингу та лендінгу юридичної компанії",
                    direction: "outbound", durationSec: 310, outcome: "Заплановано офлайн зустріч у вівторок",
                    discussionId: disc2Id,
                    relatedProducts: new[] { new RelatedProduct(1, "Landing Page"), new RelatedProduct(4, "Розробка логотипу та базового брендбуку") }),

                // 3. Зустріч офлайн (АгроТрейд Захід)
                CreateActivity(
                    "653000000000000000000003", "meeting", 1, "ТОВ «АгроТрейд Захід»", author1,
                    "Презентація структури проекту та узгодження технічного завдання на портал.",
                    new Rating(5), dealId: 1, dealTitle: "Корпоративний портал для АгроТрейд Захід",
                    location: "м. Львів, вул. Городоцька, 120, офіс 402",
                    participants: new[] { "Петро Іванчук", "Андрій Мельник", "Вікторія Коваль" },
                    agenda: "1. Прототип головної сторінки. 2. Архітектура бази даних. 3. Пакет хостингу та SSL.",
                    outcome: "ТЗ затверджено клієнтом",
                    comments: new[] { new Comment("Вікторія Коваль", "Протокол зустрічі підписано обома сторонами.") }),

                // 4. Електронний лист (Сидоренко та партнери)
                CreateActivity(
                    "653000000000000000000004", "email", 2, "Адвокатське об'єднання «Сидоренко та партнери»", author4,
                    "Надсилаємо перші 3 варіанти логотипу для юридичного бюро на вибір.",
                    new Rating(4), dealId: 2, dealTitle: "Розробка брендингу та лендінгу юридичної компанії",
                    subject: "Варіанти логотипу — АО «Сидоренко та партнери»",
                    attachments: new[] { "logo_concept_1.pdf", "logo_concept_2.pdf", "logo_concept_3.pdf" },
                    relatedProducts: new[] { new RelatedProduct(4, "Розробка логотипу та базового брендбуку") }),

                // 5. Завдання (АгроТрейд Захід)
                CreateActivity(
                    "653000000000000000000005", "task", 1, "ТОВ «АгроТрейд Захід»", author2,
                    "Підготувати договір та рахунок на 50% передоплати розробки порталу.",
                    new Rating(5), dealId: 1, dealTitle: "Корпоративний портал для АгроТрейд Захід",
                    dueDate: DateTime.UtcNow.AddDays(2), done: true,
                    comments: new[] { new Comment("Вікторія Коваль", "Рахунок №44 відправлено бухгалтерії клієнта.") }),

                // 6. Нотатка (Mon Ami)
                CreateActivity(
                    "653000000000000000000006", "note", 3, "Ресторан «Mon Ami»", author1,
                    "Клієнт шукає комплексне SMM просування для залучення гостей на осіннє сезонне меню.",
                    new Rating(4), dealId: 3, dealTitle: "SMM та таргетована реклама для ресторану",
                    relatedProducts: new[] { new RelatedProduct(6, "Комплексне ведення Instagram та Facebook (місяць)") }),

                // 7. Дзвінок вхідний (Титан)
                CreateActivity(
                    "653000000000000000000007", "call", 4, "Мережа фітнес-клубів «Титан»", author3,
                    "Запит на розробку мобільного веб-додатку для бронювання занять з тренерами.",
                    new Rating(5), dealId: 4, dealTitle: "Веб-додаток онлайн-запису до фітнес-клубів",
                    direction: "inbound", durationSec: 540,
                    outcome: "Надіслати бриф для заповнення технічних вимог"),

                // 8. Електронний лист (Mon Ami)
                CreateActivity(
                    "653000000000000000000008", "email", 3, "Ресторан «Mon Ami»", author2,
                    "Комерційна пропозиція на налаштування реклами Meta Ads та фотозйомку страв.",
                    new Rating(4), dealId: 3, dealTitle: "SMM та таргетована реклама для ресторану",
                    subject: "Комерційна пропозиція з маркетингу для ресторану Mon Ami",
                    attachments: new[] { "kp_mon_ami_marketing.pdf" },
                    relatedProducts: new[] { new RelatedProduct(7, "Налаштування таргетованої реклами Meta Ads") }),

                // 9. Зустріч онлайн (IT-кластер Захід)
                CreateActivity(
                    "653000000000000000000009", "meeting", 5, "Громадська організація «IT-кластер Захід»", author1,
                    "Онлайн-сесія в Google Meet: розробка лендінгу для щорічної IT-конференції.",
                    new Rating(5), dealId: 5, dealTitle: "Лендінг та реклама для щорічної конференції",
                    location: "Google Meet: https://meet.google.com/xyz-abcd-efg",
                    participants: new[] { "Олена Ковальчук", "Андрій Мельник" },
                    agenda: "1. Концепція лендінгу. 2. Інтеграція платіжного шлюзу Monobank.",
                    outcome: "Узгоджено терміни релізу до 15 листопада",
                    relatedProducts: new[] { new RelatedProduct(1, "Landing Page") }),

                // 10. Завдання (Сидоренко та партнери)
                CreateActivity(
                    "653000000000000000000010", "task", 2, "Адвокатське об'єднання «Сидоренко та партнери»", author4,
                    "Завершити підготовку векторних файлів логотипу для друку поліграфії.",
                    new Rating(4), dealId: 2, dealTitle: "Розробка брендингу та лендінгу юридичної компанії",
                    dueDate: DateTime.UtcNow.AddDays(1), done: false),

                // 11. Нотатка (ЕкоФарм)
                CreateActivity(
                    "653000000000000000000011", "note", 6, "ТОВ «ЕкоФарм Дистриб'юшн»", author2,
                    "Проведено первинний аудит: компанія зацікавлена у великому інтернет-магазині B2B.",
                    new Rating(5),
                    relatedProducts: new[] { new RelatedProduct(3, "Інтернет-магазин (E-commerce)") }),

                // 12. Дзвінок вихідний (АгроТрейд Захід)
                CreateActivity(
                    "653000000000000000000012", "call", 1, "ТОВ «АгроТрейд Захід»", author3,
                    "Технічна консультація щодо налаштування DNS записів та поштових серверів.",
                    new Rating(5), dealId: 1, dealTitle: "Корпоративний портал для АгроТрейд Захід",
                    direction: "outbound", durationSec: 180, outcome: "Записи налаштовано, пошта працює",
                    relatedProducts: new[] { new RelatedProduct(9, "Пакет хостингу та SSL-сертифікат на 1 рік") }),

                // 13. Дзвінок вхідний (IT-кластер Захід)
                CreateActivity(
                    "653000000000000000000013", "call", 5, "Громадська організація «IT-кластер Захід»", author2,
                    "Запит на додатковий банер для інформаційних партнерів конференції.",
                    new Rating(5), dealId: 5, dealTitle: "Лендінг та реклама для щорічної конференції",
                    direction: "inbound", durationSec: 240, outcome: "Передано в роботу дизайнеру",
                    relatedProducts: new[] { new RelatedProduct(5, "Банерна реклама та креативи (пакет 5 шт)") })
            };

            await _context.Activities.InsertManyAsync(items, cancellationToken: cancellationToken);
            _logger.LogInformation("Successfully seeded {Count} activities into MongoDB.", items.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to seed activities. MongoDB might be unavailable at startup.");
        }
    }

    private static Activity CreateActivity(
        string idHex,
        string type,
        long clientId,
        string clientName,
        Author author,
        string text,
        Rating rating,
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
        IEnumerable<Comment>? comments = null,
        IEnumerable<RelatedProduct>? relatedProducts = null)
    {
        var activity = new Activity(
            type: type,
            clientId: clientId,
            clientName: clientName,
            author: author,
            text: text,
            rating: rating,
            dealId: dealId,
            dealTitle: dealTitle,
            discussionId: discussionId,
            direction: direction,
            durationSec: durationSec,
            outcome: outcome,
            subject: subject,
            attachments: attachments,
            location: location,
            participants: participants,
            agenda: agenda,
            dueDate: dueDate,
            done: done,
            relatedProducts: relatedProducts);

        // Встановлення фіксованого ObjectId через Reflection для повної відповідності seed.js
        var idProp = typeof(BaseEntity).GetProperty(nameof(BaseEntity.Id));
        idProp?.SetValue(activity, new ObjectId(idHex));

        if (comments != null)
        {
            foreach (var comment in comments)
            {
                activity.AddComment(comment.Author, comment.Text);
            }
        }

        return activity;
    }
}
