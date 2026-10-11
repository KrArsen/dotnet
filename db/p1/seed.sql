-- ============================================================================
-- Скрипт: seed.sql (SalesDb)
-- Призначення: Ідемпотентне наповнення тестовими даними CRM (Sales Bounded Context)
--              4 співробітники, 7 клієнтів, 10 реплік товарів, 7 угод на різних етапах
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE SalesDb;
GO

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================================
-- 1. Наповнення TeamMembers (Команда студії)
-- ============================================================================
SET IDENTITY_INSERT dbo.TeamMembers ON;

MERGE dbo.TeamMembers AS target
USING (
    VALUES
        (1, N'Андрій Мельник', N'andriy.melnyk@webstudio.ua', N'Owner', 1, 0),
        (2, N'Вікторія Коваль', N'viktoriya.koval@webstudio.ua', N'SalesManager', 1, 0),
        (3, N'Денис Бондаренко', N'denys.bondarenko@webstudio.ua', N'SalesManager', 1, 0),
        (4, N'Марина Савченко', N'maryna.savchenko@webstudio.ua', N'AccountManager', 1, 0)
) AS src (Id, FullName, Email, Role, IsActive, IsDeleted)
ON (target.Id = src.Id)
WHEN MATCHED THEN
    UPDATE SET target.FullName = src.FullName, target.Role = src.Role, target.IsActive = src.IsActive, target.IsDeleted = src.IsDeleted, target.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, FullName, Email, Role, IsActive, CreatedAt, CreatedBy, IsDeleted)
    VALUES (src.Id, src.FullName, src.Email, src.Role, src.IsActive, SYSUTCDATETIME(), N'seed', src.IsDeleted);

SET IDENTITY_INSERT dbo.TeamMembers OFF;
GO

-- ============================================================================
-- 2. Наповнення Clients (Клієнти студії: 7 контрагентів)
-- ============================================================================
SET IDENTITY_INSERT dbo.Clients ON;

MERGE dbo.Clients AS target
USING (
    VALUES
        (1, N'ТОВ «АгроТрейд Захід»', N'Петро Іванчук', N'ivanchuk@agrotrade.ua', N'+380671112233', N'Агробізнес'),
        (2, N'Юридичне бюро «Сидоренко та партнери»', N'Марія Сидоренко', N'maria@sydorenko-law.com', N'+380502223344', N'Юридичні послуги'),
        (3, N'ТОВ «Смарт Логістика»', N'Василь Грищенко', N'v.gryshchenko@smartlog.ua', N'+380933334455', N'Транспорт та логістика'),
        (4, N'ПП «Еко Продукти»', N'Світлана Шевчук', N'svitlana@eco-goods.com', N'+380674445566', N'Роздрібна торгівля'),
        (5, N'ТОВ «Інвест Груп»', N'Олег Тарасов', N'o.tarasov@investgroup.ua', N'+380507778899', N'Фінансові послуги'),
        (6, N'IT-стартап «CloudFlow»', N'Ірина Кравчук', N'irina@cloudflow.io', N'+380631234567', N'Інформаційні технології'),
        (7, N'Мережа клінік «МедАльянс»', N'Олександр Романюк', N'romanyuk@medalliance.com.ua', N'+380679876543', N'Охорона здоров''я')
) AS src (Id, CompanyName, ContactPerson, Email, Phone, Industry)
ON (target.Id = src.Id)
WHEN MATCHED THEN
    UPDATE SET 
        target.CompanyName = src.CompanyName,
        target.ContactPerson = src.ContactPerson,
        target.Phone = src.Phone,
        target.Industry = src.Industry,
        target.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, CompanyName, ContactPerson, Email, Phone, Industry, CreatedAt, CreatedBy, IsDeleted)
    VALUES (src.Id, src.CompanyName, src.ContactPerson, src.Email, src.Phone, src.Industry, SYSUTCDATETIME(), N'seed', 0);

SET IDENTITY_INSERT dbo.Clients OFF;
GO

-- ============================================================================
-- 3. Наповнення Products (Локальна проекція довідника Прайс-листа)
-- Ідентифікатори 1..10 суворо збігаються з CatalogDb та ActivitiesDb
-- ============================================================================
MERGE dbo.Products AS target
USING (
    VALUES
        (1, N'Лендінг під ключ', CAST(25000.00 AS DECIMAL(18,2)), 1),
        (2, N'Корпоративний сайт', CAST(65000.00 AS DECIMAL(18,2)), 1),
        (3, N'Технічна підтримка сайту (1 місяць)', CAST(12000.00 AS DECIMAL(18,2)), 1),
        (4, N'Комплексний SEO-аудит сайту', CAST(15000.00 AS DECIMAL(18,2)), 1),
        (5, N'Щомісячне SEO-просування', CAST(22000.00 AS DECIMAL(18,2)), 1),
        (6, N'Ведення та просування Instagram (1 місяць)', CAST(18000.00 AS DECIMAL(18,2)), 1),
        (7, N'Розробка логотипу та фірмового стилю', CAST(28000.00 AS DECIMAL(18,2)), 1),
        (8, N'Налаштування таргетованої та контекстної реклами', CAST(14000.00 AS DECIMAL(18,2)), 1),
        (9, N'Пакет хостингу та SSL-сертифікат на 1 рік', CAST(4500.00 AS DECIMAL(18,2)), 1),
        (10, N'Ліцензія CMS Pro для клієнта', CAST(9800.00 AS DECIMAL(18,2)), 1)
) AS src (Id, Name, Price, IsActive)
ON (target.Id = src.Id)
WHEN MATCHED THEN
    UPDATE SET target.Name = src.Name, target.Price = src.Price, target.IsActive = src.IsActive, target.UpdatedAt = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (Id, Name, Price, IsActive, CreatedAt, CreatedBy)
    VALUES (src.Id, src.Name, src.Price, src.IsActive, SYSUTCDATETIME(), N'seed');
GO

-- ============================================================================
-- 4. Наповнення 7 тестових угод (Deals, DealDetails, DealItems, DealStatusHistory)
-- Використовуємо явні Id (IDENTITY_INSERT ON), щоб збігалися з MongoDB
-- ============================================================================
SET IDENTITY_INSERT dbo.Deals ON;

-- 4.1. Угода №1: DL-20261001-1001 (Статус: Won)
IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = 1)
BEGIN
    INSERT INTO dbo.Deals (Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (1, N'DL-20261001-1001', N'Корпоративний портал для АгроТрейд Захід', 1, 2, N'Won', 69500.00, '2026-10-01T09:00:00', N'seed', 0);

    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (1, N'Website', N'Розробка сучасного сайту компанії з каталогом зернових культур та хостингом.', NULL, '2026-10-05T18:00:00', '2026-10-01T09:00:00', N'seed');

    INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity, CreatedAt, CreatedBy)
    VALUES 
        (1, 2, N'Корпоративний сайт', 65000.00, 0.00, 1, '2026-10-01T09:00:00', N'seed'),
        (1, 9, N'Пакет хостингу та SSL-сертифікат на 1 рік', 4500.00, 0.00, 1, '2026-10-01T09:00:00', N'seed');

    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES 
        (1, NULL, N'Lead', '2026-10-01T09:00:00', N'seed', N'Первинне звернення через форму на сайті'),
        (1, N'Lead', N'Qualified', '2026-10-02T11:00:00', N'seed', N'Кваліфікація: бюджет узгоджено, ТЗ отримано'),
        (1, N'Qualified', N'ProposalSent', '2026-10-03T15:30:00', N'seed', N'КП відправлено клієнту на email'),
        (1, N'ProposalSent', N'Negotiation', '2026-10-04T12:00:00', N'seed', N'Узгодження етапів оплати та дедлайнів'),
        (1, N'Negotiation', N'Won', '2026-10-05T16:45:00', N'seed', N'Договір підписано, отримано 50% передоплати');
END;

-- 4.2. Угода №2: DL-20261005-1002 (Статус: ProposalSent)
IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = 2)
BEGIN
    INSERT INTO dbo.Deals (Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (2, N'DL-20261005-1002', N'Створення лендінгу та айдентики для адвокатів', 2, 2, N'ProposalSent', 50350.00, '2026-10-05T10:30:00', N'seed', 0);

    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (2, N'Referral', N'Розробка посадкової сторінки під ключ зі знижкою 2650 грн на пакет брендингу.', NULL, '2026-10-20T18:00:00', '2026-10-05T10:30:00', N'seed');

    INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity, CreatedAt, CreatedBy)
    VALUES 
        (2, 1, N'Лендінг під ключ', 25000.00, 1250.00, 1, '2026-10-05T10:30:00', N'seed'),
        (2, 7, N'Розробка логотипу та фірмового стилю', 28000.00, 1400.00, 1, '2026-10-05T10:30:00', N'seed');

    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES 
        (2, NULL, N'Lead', '2026-10-05T10:30:00', N'seed', N'Рекомендація від існуючого клієнта'),
        (2, N'Lead', N'Qualified', '2026-10-06T14:00:00', N'seed', N'Проведено вступний брифінг'),
        (2, N'Qualified', N'ProposalSent', '2026-10-07T17:00:00', N'seed', N'Надіслано кошторис та комерційну пропозицію');
END;

-- 4.3. Угода №3: DL-20261010-1003 (Статус: Lead)
IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = 3)
BEGIN
    INSERT INTO dbo.Deals (Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (3, N'DL-20261010-1003', N'SEO аудит логістичного сайту Smart Logistics', 3, 3, N'Lead', 0.00, '2026-10-10T12:00:00', N'seed', 0);

    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (3, N'ColdOutreach', N'Клієнт зацікавився оптимізацією конверсії та пошуковим аудитом.', NULL, '2026-10-25T18:00:00', '2026-10-10T12:00:00', N'seed');

    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES (3, NULL, N'Lead', '2026-10-10T12:00:00', N'seed', N'Первинний контакт у LinkedIn');
END;

-- 4.4. Угода №4: DL-20261008-1004 (Статус: Lost)
IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = 4)
BEGIN
    INSERT INTO dbo.Deals (Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (4, N'DL-20261008-1004', N'Редизайн інтернет-магазину Еко Продукти', 4, 3, N'Lost', 65000.00, '2026-10-04T11:00:00', N'seed', 0);

    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (4, N'Website', N'Комплексний редизайн на OpenCart.', N'Клієнт заморозив маркетинговий бюджет до наступного кварталу через реорганізацію', '2026-10-15T18:00:00', '2026-10-04T11:00:00', N'seed');

    INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity, CreatedAt, CreatedBy)
    VALUES (4, 2, N'Корпоративний сайт', 65000.00, 0.00, 1, '2026-10-04T11:00:00', N'seed');

    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES 
        (4, NULL, N'Lead', '2026-10-04T11:00:00', N'seed', N'Запит на редизайн'),
        (4, N'Lead', N'Qualified', '2026-10-05T13:00:00', N'seed', N'Кваліфіковано вимоги'),
        (4, N'Qualified', N'Lost', '2026-10-08T16:20:00', N'seed', N'Клієнт повідомив про призупинення проєкту');
END;

-- 4.5. Угода №5: DL-20261009-1005 (Статус: Negotiation)
IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = 5)
BEGIN
    INSERT INTO dbo.Deals (Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (5, N'DL-20261009-1005', N'SMM просування та таргет для Invest Group', 5, 2, N'Negotiation', 32000.00, '2026-10-03T14:00:00', N'seed', 0);

    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (5, N'Inbound', N'Комплексне ведення соцмереж та налаштування таргетованої реклами.', NULL, '2026-10-18T18:00:00', '2026-10-03T14:00:00', N'seed');

    INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity, CreatedAt, CreatedBy)
    VALUES 
        (5, 6, N'Ведення та просування Instagram (1 місяць)', 18000.00, 0.00, 1, '2026-10-03T14:00:00', N'seed'),
        (5, 8, N'Налаштування таргетованої та контекстної реклами', 14000.00, 0.00, 1, '2026-10-03T14:00:00', N'seed');

    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES 
        (5, NULL, N'Lead', '2026-10-03T14:00:00', N'seed', N'Дзвінок від директора з маркетингу'),
        (5, N'Lead', N'Qualified', '2026-10-04T10:00:00', N'seed', N'Брифінг за цілями кампанії'),
        (5, N'Qualified', N'ProposalSent', '2026-10-06T12:00:00', N'seed', N'Презентація стратегії та медіаплану'),
        (5, N'ProposalSent', N'Negotiation', '2026-10-09T15:00:00', N'seed', N'Погодження KPI за кількістю лідів');
END;

-- 4.6. Угода №6: DL-20261007-1006 (Статус: Qualified)
IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = 6)
BEGIN
    INSERT INTO dbo.Deals (Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (6, N'DL-20261007-1006', N'Розробка айдентики для CloudFlow', 6, 3, N'Qualified', 28000.00, '2026-10-07T11:00:00', N'seed', 0);

    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (6, N'Website', N'Розробка логотипу та гайдлайнів для хмарного сервісу.', NULL, '2026-10-30T18:00:00', '2026-10-07T11:00:00', N'seed');

    INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity, CreatedAt, CreatedBy)
    VALUES 
        (6, 7, N'Розробка логотипу та фірмового стилю', 28000.00, 0.00, 1, '2026-10-07T11:00:00', N'seed');

    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES 
        (6, NULL, N'Lead', '2026-10-07T11:00:00', N'seed', N'Заповнена форма на сайті'),
        (6, N'Lead', N'Qualified', '2026-10-08T16:00:00', N'seed', N'Узгоджено базовий референс та стилістику');
END;

-- 4.7. Угода №7: DL-20261011-1007 (Статус: Lead)
IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = 7)
BEGIN
    INSERT INTO dbo.Deals (Id, DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (7, N'DL-20261011-1007', N'Підтримка веб-ресурсу клініки МедАльянс', 7, 2, N'Lead', 0.00, '2026-10-11T09:30:00', N'seed', 0);

    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (7, N'Referral', N'Запит на щомісячний супровід та оновлення CMS.', NULL, '2026-11-10T18:00:00', '2026-10-11T09:30:00', N'seed');

    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES (7, NULL, N'Lead', '2026-10-11T09:30:00', N'seed', N'Первинний вхідний дзвінок');
END;

SET IDENTITY_INSERT dbo.Deals OFF;
GO

PRINT N'[SEED] Базу даних SalesDb успішно наповнено (7 клієнтів, 4 менеджери, 10 товарів, 7 угод).';
GO
