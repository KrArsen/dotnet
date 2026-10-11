-- ============================================================================
-- Скрипт: crud_examples.sql (SalesDb)
-- Призначення: Приклади CRUD-операцій для Clients, Products та Deals,
--              пагінація (OFFSET/FETCH), пошук та звіт за воронкою продажів
-- Контекст: Sales Bounded Context (CRM для малого бізнесу)
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE SalesDb;
GO

SET NOCOUNT ON;
GO

-- ============================================================================
-- 1. CRUD ДЛЯ CLIENTS (КЛІЄНТИ)
-- ============================================================================

-- 1.1. Create (Створення нового контрагента)
INSERT INTO dbo.Clients (CompanyName, ContactPerson, Email, Phone, Industry, CreatedBy)
VALUES (N'ТОВ «Інноваційні Технології»', N'Олександр Воронін', N'o.voronin@innotech.ua', N'+380508889900', N'IT / FinTech', N'ManagerPortal');

DECLARE @NewClientId BIGINT = SCOPE_IDENTITY();
SELECT * FROM dbo.Clients WHERE Id = @NewClientId;

-- 1.2. Read (Пошук за назвою компанії або email з пагінацією)
DECLARE @SearchPattern NVARCHAR(100) = N'%Агро%';
DECLARE @PageNumber INT = 1;
DECLARE @PageSize INT = 5;

SELECT 
    Id,
    CompanyName,
    ContactPerson,
    Email,
    Phone,
    Industry,
    CreatedAt
FROM dbo.Clients
WHERE IsDeleted = 0
  AND (CompanyName LIKE @SearchPattern OR Email LIKE @SearchPattern)
ORDER BY CompanyName ASC
OFFSET (@PageNumber - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;

-- 1.3. Update з оптимістичним блокуванням (Optimistic Concurrency via RowVersion)
-- Читаємо поточну версію рядка клієнта
DECLARE @CurrentRowVersion ROWVERSION;
SELECT @CurrentRowVersion = RowVersion FROM dbo.Clients WHERE Id = @NewClientId;

-- Оновлюємо дані, гарантуючи, що рядок не був модифікований паралельним процесом
UPDATE dbo.Clients
SET ContactPerson = N'Олександр В. Воронін',
    Phone = N'+380501119999',
    UpdatedAt = SYSUTCDATETIME(),
    UpdatedBy = N'ManagerPortal'
WHERE Id = @NewClientId 
  AND RowVersion = @CurrentRowVersion;

IF @@ROWCOUNT = 0
BEGIN
    THROW 50009, N'Конфлікт паралельного доступу (Concurrency conflict): клієнта було змінено іншим процесом!', 1;
END;

-- 1.4. Soft Delete (М'яке видалення клієнта зі збереженням аудиту)
UPDATE dbo.Clients
SET IsDeleted = 1,
    UpdatedAt = SYSUTCDATETIME(),
    UpdatedBy = N'GdprAdmin'
WHERE Id = @NewClientId;

-- Перевірка: Soft Deleted записи відфільтровуються при звичайному читанні (WHERE IsDeleted = 0)
SELECT Id, CompanyName, IsDeleted, UpdatedAt, UpdatedBy
FROM dbo.Clients
WHERE Id = @NewClientId AND IsDeleted = 0; -- Повертає 0 рядків!

-- ============================================================================
-- 2. CRUD ДЛЯ PRODUCTS (ЛОКАЛЬНА ПРОЕКЦІЯ КАТАЛОГУ)
-- ============================================================================

-- 2.1. Create / Sync (Отримання нової позиції з доменної події каталогу)
IF NOT EXISTS (SELECT 1 FROM dbo.Products WHERE Id = 11)
BEGIN
    INSERT INTO dbo.Products (Id, Name, Price, IsActive, CreatedBy)
    VALUES (11, N'Консультація з цифрової трансформації (2 години)', 8000.00, 1, N'CatalogEventConsumer');
END;

-- 2.2. Read (Перелік активних послуг студії)
SELECT Id, Name, Price, IsActive
FROM dbo.Products
WHERE IsActive = 1
ORDER BY Price DESC;

-- 2.3. Update (Індексація ціни після події CatalogPriceChangedEvent)
UPDATE dbo.Products
SET Price = 8500.00,
    UpdatedAt = SYSUTCDATETIME(),
    UpdatedBy = N'CatalogEventConsumer'
WHERE Id = 11;

-- 2.4. Deactivate (Деактивація послуги замість фізичного видалення)
UPDATE dbo.Products
SET IsActive = 0,
    UpdatedAt = SYSUTCDATETIME(),
    UpdatedBy = N'CatalogEventConsumer'
WHERE Id = 11;

-- ============================================================================
-- 3. CRUD ДЛЯ DEALS ТА ЗВІТНІСТЬ ВОРОНКИ ПРОДАЖІВ
-- ============================================================================

-- 3.1. Read через представлення vw_DealSummary (Угода + Клієнт + Менеджер + Суми)
SELECT 
    DealId,
    DealNumber,
    Title,
    Stage,
    ClientCompanyName,
    OwnerName,
    ItemsCount,
    CalculatedTotal,
    ExpectedCloseDate,
    LostReason
FROM dbo.vw_DealSummary
ORDER BY DealId DESC;

-- 3.2. Фільтрація угод за менеджером та діапазоном сум з пагінацією
DECLARE @OwnerId BIGINT = 2; -- Вікторія Коваль
DECLARE @MinAmount DECIMAL(18,2) = 10000.00;

SELECT 
    d.Id,
    d.DealNumber,
    d.Title,
    d.Stage,
    d.TotalAmount,
    c.CompanyName
FROM dbo.Deals d
JOIN dbo.Clients c ON d.ClientId = c.Id
WHERE d.IsDeleted = 0
  AND d.OwnerId = @OwnerId
  AND d.TotalAmount >= @MinAmount
ORDER BY d.TotalAmount DESC
OFFSET 0 ROWS
FETCH NEXT 10 ROWS ONLY;

-- 3.3. Аналітичний звіт: Кількість та загальна сума угод за етапами воронки
SELECT 
    d.Stage AS FunnelStage,
    COUNT(d.Id) AS DealsCount,
    ISNULL(SUM(d.TotalAmount), 0.00) AS TotalFunnelAmount,
    CAST(AVG(d.TotalAmount) AS DECIMAL(18,2)) AS AverageDealAmount
FROM dbo.Deals d
WHERE d.IsDeleted = 0
GROUP BY d.Stage
ORDER BY 
    CASE d.Stage
        WHEN N'Lead' THEN 1
        WHEN N'Qualified' THEN 2
        WHEN N'ProposalSent' THEN 3
        WHEN N'Negotiation' THEN 4
        WHEN N'Won' THEN 5
        WHEN N'Lost' THEN 6
        ELSE 7
    END;
GO
