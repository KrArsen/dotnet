using Catalog.Dal.Context;
using Catalog.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Catalog.Dal.Seeding;

public static class DbInitializer
{
    public static async Task SeedAsync(AppDbContext context, ILogger? logger = null, CancellationToken ct = default)
    {
        // Ідемпотентна перевірка: якщо продукти вже є, сідінг пропускається
        if (await context.Products.AnyAsync(ct))
        {
            logger?.LogInformation("База даних CatalogDb вже містить позиції каталогу. Сідінг пропущено.");
            return;
        }

        logger?.LogInformation("Початок програмного сідінгу CatalogDb (продукти, деталі, зображення, M:N зв'язки)...");

        // 1. Створення 10 продуктів студії веб-розробки та маркетингу (відповідають Лаб 01 db/p2/seed.sql)
        var p1 = new Product("SRV-LAND-01", "Лендінг під ключ", "Service", 25000.00m, 50, "seed");
        var p2 = new Product("SRV-CORP-02", "Корпоративний сайт", "Service", 65000.00m, 30, "seed");
        var p3 = new Product("SRV-SUPP-03", "Технічна підтримка сайту (1 місяць)", "Service", 12000.00m, 100, "seed");
        var p4 = new Product("SRV-SEOA-04", "Комплексний SEO-аудит сайту", "Service", 15000.00m, 40, "seed");
        var p5 = new Product("SRV-SEOM-05", "Щомісячне SEO-просування", "Service", 22000.00m, 60, "seed");
        var p6 = new Product("SRV-SMMM-06", "Ведення та просування Instagram (1 місяць)", "Service", 18000.00m, 50, "seed");
        var p7 = new Product("SRV-LOGO-07", "Розробка логотипу та фірмового стилю", "Service", 28000.00m, 40, "seed");
        var p8 = new Product("SRV-ADVP-08", "Налаштування таргетованої та контекстної реклами", "Service", 14000.00m, 50, "seed");
        var p9 = new Product("PRD-HOST-09", "Пакет хостингу та SSL-сертифікат на 1 рік", "Product", 4500.00m, 200, "seed");
        var p10 = new Product("PRD-CMSL-10", "Ліцензія CMS Pro для клієнта", "Product", 9800.00m, 150, "seed");

        var products = new[] { p1, p2, p3, p4, p5, p6, p7, p8, p9, p10 };
        await context.Products.AddRangeAsync(products, ct);
        await context.SaveChangesAsync(ct);

        // 2. Деталі специфікації (1:1 ProductDetail)
        var d1 = new ProductDetail(p1, "Розробка адаптивного односторінкового сайту на Vue.js з формою лідогенерації, аналітикою та базовим SEO.", "проєкт", 4800, 20.00m, "seed");
        var d2 = new ProductDetail(p2, "Багатосторінковий бізнес-сайт з каталогом продукції/послуг, панеллю керування, кастомним дизайном та безпекою.", "проєкт", 9600, 20.00m, "seed");
        var d3 = new ProductDetail(p3, "Регулярне оновлення модулів безпеки, резервне копіювання, моніторинг аптайму 24/7 та до 10 годин правок розробника.", "місяць", 1200, 20.00m, "seed");
        var d4 = new ProductDetail(p4, "Глибокий технічний аудит швидкості, індексації, мобільної адаптивності, посилального профілю та чек-лист рекомендацій.", "послуга", 1800, 20.00m, "seed");
        var d5 = new ProductDetail(p5, "Щомісячний комплекс робіт: закупівля якісних посилань, нарощування маси, оптимізація метатегів та моніторинг позицій.", "місяць", 2400, 20.00m, "seed");
        var d6 = new ProductDetail(p6, "Створення 12 постів, 40 сторіз, модерація директу, написання контент-плану та підготовка візуального стилю сітки.", "місяць", 2400, 20.00m, "seed");
        var d7 = new ProductDetail(p7, "Створення 3 унікальних концепцій логотипу, підбір фірмових кольорів, шрифтової пари та оформлення брендбуку (PDF).", "проєкт", 3600, 20.00m, "seed");
        var d8 = new ProductDetail(p8, "Створення рекламних кабінетів у Meta Ads та Google Ads, аналіз аудиторій, запуск лідогенерації та A/B спліт-тести.", "послуга", 1500, 20.00m, "seed");
        var d9 = new ProductDetail(p9, "Виділений SSD-хостинг у європейському дата-центрі, необмежений трафік та преміум SSL-сертифікат строком на 1 рік.", "рік", null, 20.00m, "seed");
        var d10 = new ProductDetail(p10, "Безстрокова комерційна ліцензія на CMS з доступом до офіційних оновлень безпеки та модулів на 1 домен.", "ліцензія", null, 20.00m, "seed");

        await context.ProductDetails.AddRangeAsync(new[] { d1, d2, d3, d4, d5, d6, d7, d8, d9, d10 }, ct);

        // 3. Зображення продуктів (1:N ProductImage)
        var images = new List<ProductImage>
        {
            new(p1, "https://cdn.studio.ua/images/landing-page-hero.webp", "Головний екран лендінгу", true, 1),
            new(p1, "https://cdn.studio.ua/images/landing-page-mobile.webp", "Мобільна версія лендінгу", false, 2),
            new(p2, "https://cdn.studio.ua/images/corporate-portal.webp", "Корпоративний портал", true, 1),
            new(p3, "https://cdn.studio.ua/images/support-dashboard.webp", "Панель технічної підтримки", true, 1),
            new(p4, "https://cdn.studio.ua/images/seo-audit-report.webp", "Звіт аудиту SEO", true, 1),
            new(p5, "https://cdn.studio.ua/images/seo-analytics.webp", "Аналітика позицій сайту", true, 1),
            new(p6, "https://cdn.studio.ua/images/smm-grid.webp", "Приклад Instagram сітки", true, 1),
            new(p7, "https://cdn.studio.ua/images/brandbook-mockup.webp", "Мокап брендбуку", true, 1),
            new(p8, "https://cdn.studio.ua/images/ads-dashboard.webp", "Кабінет таргетованої реклами", true, 1),
            new(p9, "https://cdn.studio.ua/images/hosting-tier.webp", "Серверний хостинг", true, 1),
            new(p10, "https://cdn.studio.ua/images/cms-license-cert.webp", "Електронний сертифікат ліцензії", true, 1)
        };
        await context.ProductImages.AddRangeAsync(images, ct);

        // 4. M:N зв'язки ProductCategory (зі значенням AddedAt)
        var categories = await context.Categories.ToDictionaryAsync(c => c.Id, ct);

        var productCategories = new List<ProductCategory>
        {
            new(p1, categories[1]), // Лендінг -> Веб-розробка
            new(p2, categories[1]), // Корпоративний сайт -> Веб-розробка
            new(p3, categories[2]), // Підтримка -> Техпідтримка
            new(p4, categories[3]), // SEO-аудит -> SEO
            new(p5, categories[3]), // Щомісячне SEO -> SEO
            new(p6, categories[4]), // Instagram -> Маркетинг
            new(p7, categories[5]), // Логотип -> Дизайн
            new(p8, categories[4]), // Реклама -> Маркетинг
            new(p9, categories[6]), // Хостинг -> Хмарна інфраструктура
            new(p10, categories[6]) // Ліцензія CMS -> Хмарна інфраструктура
        };
        await context.ProductCategories.AddRangeAsync(productCategories, ct);

        await context.SaveChangesAsync(ct);
        logger?.LogInformation("Програмний сідінг CatalogDb успішно завершено: 10 продуктів, деталі, зображення та M:N зв'язки додано.");
    }
}
