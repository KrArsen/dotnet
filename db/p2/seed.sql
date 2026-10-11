-- ============================================================================
-- Скрипт: seed.sql (CatalogDb)
-- Призначення: Ідемпотентне наповнення прайс-листа веб-студії (10 позицій, 5 категорій, історія цін)
-- Контекст: Catalog Bounded Context
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE CatalogDb;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================================
-- 1. Наповнення Category (Категорії послуг та товарів)
-- ============================================================================
SET IDENTITY_INSERT dbo.Category ON;

MERGE dbo.Category AS target
USING (
    VALUES
        (1, N'Веб-розробка', N'Створення односторінкових сайтів, порталів та веб-сервісів'),
        (2, N'Технічна підтримка', N'Супровід, оновлення безпеки та моніторинг стабільності'),
        (3, N'Пошукова оптимізація (SEO)', N'Технічний аудит, оптимізація семантики та просування'),
        (4, N'Маркетинг та реклама (SMM/PPC)', N'Таргетована реклама, контекст та просування в соцмережах'),
        (5, N'Дизайн та айдентика', N'Розробка логотипів, брендбуків та користувацьких інтерфейсів UI/UX'),
        (6, N'Хмарна інфраструктура та ліцензії', N'Хостинг, SSL-сертифікати та ліцензійне ПЗ')
) AS src (Id, Name, Description)
ON (target.Id = src.Id)
WHEN MATCHED THEN
    UPDATE SET target.Name = src.Name, target.Description = src.Description, target.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Description, CreatedAt, CreatedBy, IsDeleted)
    VALUES (src.Id, src.Name, src.Description, SYSUTCDATETIME(), N'seed', 0);

SET IDENTITY_INSERT dbo.Category OFF;
GO

-- ============================================================================
-- 2. Наповнення Product (10 позицій прайс-листа веб-студії)
-- Ідентифікатори 1..10 суворо збігаються з SalesDb.Products та ActivitiesDb
-- ============================================================================
SET IDENTITY_INSERT dbo.Product ON;

MERGE dbo.Product AS target
USING (
    VALUES
        (1, N'SRV-LAND-01', N'Лендінг під ключ', N'Service', CAST(25000.00 AS DECIMAL(18,2)), 1),
        (2, N'SRV-CORP-02', N'Корпоративний сайт', N'Service', CAST(65000.00 AS DECIMAL(18,2)), 1),
        (3, N'SRV-SUPP-03', N'Технічна підтримка сайту (1 місяць)', N'Service', CAST(12000.00 AS DECIMAL(18,2)), 1),
        (4, N'SRV-SEOA-04', N'Комплексний SEO-аудит сайту', N'Service', CAST(15000.00 AS DECIMAL(18,2)), 1),
        (5, N'SRV-SEOM-05', N'Щомісячне SEO-просування', N'Service', CAST(22000.00 AS DECIMAL(18,2)), 1),
        (6, N'SRV-SMMM-06', N'Ведення та просування Instagram (1 місяць)', N'Service', CAST(18000.00 AS DECIMAL(18,2)), 1),
        (7, N'SRV-LOGO-07', N'Розробка логотипу та фірмового стилю', N'Service', CAST(28000.00 AS DECIMAL(18,2)), 1),
        (8, N'SRV-ADVP-08', N'Налаштування таргетованої та контекстної реклами', N'Service', CAST(14000.00 AS DECIMAL(18,2)), 1),
        (9, N'PRD-HOST-09', N'Пакет хостингу та SSL-сертифікат на 1 рік', N'Product', CAST(4500.00 AS DECIMAL(18,2)), 1),
        (10, N'PRD-CMSL-10', N'Ліцензія CMS Pro для клієнта', N'Product', CAST(9800.00 AS DECIMAL(18,2)), 1)
) AS src (Id, Sku, Name, ItemType, Price, IsActive)
ON (target.Id = src.Id)
WHEN MATCHED THEN
    UPDATE SET 
        target.Sku = src.Sku,
        target.Name = src.Name,
        target.ItemType = src.ItemType,
        target.Price = src.Price,
        target.IsActive = src.IsActive,
        target.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, Sku, Name, ItemType, Price, IsActive, CreatedAt, CreatedBy, IsDeleted)
    VALUES (src.Id, src.Sku, src.Name, src.ItemType, src.Price, src.IsActive, SYSUTCDATETIME(), N'seed', 0);

SET IDENTITY_INSERT dbo.Product OFF;
GO

-- ============================================================================
-- 3. Наповнення ProductDetail (Деталі специфікації та одиниці виміру)
-- ============================================================================
MERGE dbo.ProductDetail AS target
USING (
    VALUES
        (1, N'Розробка адаптивного односторінкового сайту на Vue.js з формою лідогенерації, аналітикою та базовим SEO.', N'проєкт', 4800, 20.00),
        (2, N'Багатосторінковий бізнес-сайт з каталогом продукції/послуг, панеллю керування, кастомним дизайном та безпекою.', N'проєкт', 9600, 20.00),
        (3, N'Регулярне оновлення модулів безпеки, резервне копіювання, моніторинг аптайму 24/7 та до 10 годин правок розробника.', N'місяць', 1200, 20.00),
        (4, N'Глибокий технічний аудит швидкості, індексації, мобільної адаптивності, посилального профілю та чек-лист рекомендацій.', N'послуга', 1800, 20.00),
        (5, N'Щомісячний комплекс робіт: закупівля якісних посилань, нарощування маси, оптимізація метатегів та моніторинг позицій.', N'місяць', 2400, 20.00),
        (6, N'Створення 12 постів, 40 сторіз, модерація директу, написання контент-плану та підготовка візуального стилю сітки.', N'місяць', 2400, 20.00),
        (7, N'Створення 3 унікальних концепцій логотипу, підбір фірмових кольорів, шрифтової пари та оформлення брендбуку (PDF).', N'проєкт', 3600, 20.00),
        (8, N'Створення рекламних кабінетів у Meta Ads та Google Ads, аналіз аудиторій, запуск лідогенерації та A/B спліт-тести.', N'послуга', 1500, 20.00),
        (9, N'Виділений SSD-хостинг у європейському дата-центрі, необмежений трафік та преміум SSL-сертифікат строком на 1 рік.', N'рік', NULL, 20.00),
        (10, N'Безстрокова комерційна ліцензія на CMS з доступом до офіційних оновлень безпеки та модулів на 1 домен.', N'ліцензія', NULL, 20.00)
) AS src (ProductId, Description, Unit, DurationMinutes, TaxRate)
ON (target.ProductId = src.ProductId)
WHEN MATCHED THEN
    UPDATE SET 
        target.Description = src.Description,
        target.Unit = src.Unit,
        target.DurationMinutes = src.DurationMinutes,
        target.TaxRate = src.TaxRate,
        target.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (ProductId, Description, Unit, DurationMinutes, TaxRate, CreatedAt, CreatedBy)
    VALUES (src.ProductId, src.Description, src.Unit, src.DurationMinutes, src.TaxRate, SYSUTCDATETIME(), N'seed');
GO

-- ============================================================================
-- 4. Наповнення PriceHistory (Історія тарифів)
-- ============================================================================
MERGE dbo.PriceHistory AS target
USING (
    VALUES
        (1, 22000.00, '2026-01-01T00:00:00', '2026-06-01T00:00:00', N'Тариф першого півріччя'),
        (1, 25000.00, '2026-06-01T00:00:00', NULL, N'Планова індексація вартості розробки'),
        (2, 60000.00, '2026-01-01T00:00:00', '2026-08-01T00:00:00', N'Базовий тариф'),
        (2, 65000.00, '2026-08-01T00:00:00', NULL, N'Оновлений стек технологій та розширений скоуп'),
        (3, 12000.00, '2026-01-01T00:00:00', NULL, N'Поточний тариф підтримки'),
        (4, 15000.00, '2026-01-01T00:00:00', NULL, N'Фіксована вартість комплексного аудиту'),
        (5, 20000.00, '2026-01-01T00:00:00', '2026-05-01T00:00:00', N'Початковий тариф'),
        (5, 22000.00, '2026-05-01T00:00:00', NULL, N'Підвищення вартості лінкбілдингу'),
        (6, 18000.00, '2026-01-01T00:00:00', NULL, N'Поточний SMM пакет'),
        (7, 28000.00, '2026-01-01T00:00:00', NULL, N'Стандартний пакет айдентики'),
        (8, 14000.00, '2026-01-01T00:00:00', NULL, N'Налаштування PPC/Target'),
        (9, 4500.00, '2026-01-01T00:00:00', NULL, N'Річний пакет хостингу'),
        (10, 9800.00, '2026-01-01T00:00:00', NULL, N'Вартість ліцензії CMS')
) AS src (ProductId, Price, ValidFrom, ValidTo, Reason)
ON (target.ProductId = src.ProductId AND target.ValidFrom = src.ValidFrom)
WHEN MATCHED THEN
    UPDATE SET target.Price = src.Price, target.ValidTo = src.ValidTo, target.Reason = src.Reason
WHEN NOT MATCHED THEN
    INSERT (ProductId, Price, ValidFrom, ValidTo, ChangedBy, Reason)
    VALUES (src.ProductId, src.Price, src.ValidFrom, src.ValidTo, N'seed', src.Reason);
GO

-- ============================================================================
-- 5. Наповнення ProductCategory (M:N зв'язки)
-- ============================================================================
MERGE dbo.ProductCategory AS target
USING (
    VALUES
        (1, 1), -- Лендінг -> Веб-розробка
        (2, 1), -- Корпоративний сайт -> Веб-розробка
        (3, 2), -- Підтримка -> Техпідтримка
        (4, 3), -- SEO-аудит -> SEO
        (5, 3), -- Щомісячне SEO -> SEO
        (6, 4), -- Instagram -> Маркетинг
        (7, 5), -- Логотип -> Дизайн
        (8, 4), -- Реклама -> Маркетинг
        (9, 6), -- Хостинг -> Хмарна інфраструктура
        (10, 6) -- Ліцензія CMS -> Хмарна інфраструктура
) AS src (ProductId, CategoryId)
ON (target.ProductId = src.ProductId AND target.CategoryId = src.CategoryId)
WHEN NOT MATCHED THEN
    INSERT (ProductId, CategoryId, AddedAt)
    VALUES (src.ProductId, src.CategoryId, SYSUTCDATETIME());
GO
