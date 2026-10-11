-- ============================================================================
-- Скрипт: schema.sql (SalesDb)
-- Призначення: Створення бази даних, схеми, таблиць, обмежень, індексів та view
-- Контекст: Sales Bounded Context (CRM для малого бізнесу)
-- СУБД: Microsoft SQL Server 2022 (для ADO.NET + Dapper)
-- ============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Створення бази даних SalesDb
IF DB_ID(N'SalesDb') IS NULL
BEGIN
    CREATE DATABASE SalesDb
    COLLATE Latin1_General_100_CI_AS_SC_UTF8;
END;
GO

USE SalesDb;
GO

-- 2. Створення типу табличного параметра для передачі позицій угоди
IF TYPE_ID(N'dbo.DealItemTableType') IS NULL
BEGIN
    CREATE TYPE dbo.DealItemTableType AS TABLE
    (
        ProductId BIGINT NOT NULL,
        Quantity INT NOT NULL,
        Discount DECIMAL(18,2) NOT NULL DEFAULT 0.00
    );
END;
GO

-- ============================================================================
-- 3. ТАБЛИЦІ СХЕМИ SALES
-- ============================================================================

-- 3.1. Таблиця TeamMembers (Співробітники студії: власник, менеджери)
IF OBJECT_ID(N'dbo.TeamMembers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.TeamMembers
    (
        -- PK: BIGINT IDENTITY (послідовний, компактний кластерний індекс 8 байт для єдиного джерела вставки; GUID доречний лише для розподіленої генерації)
        Id BIGINT IDENTITY(1,1) NOT NULL,
        FullName NVARCHAR(150) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Role NVARCHAR(50) NOT NULL CONSTRAINT DF_TeamMembers_Role DEFAULT N'SalesManager',
        IsActive BIT NOT NULL CONSTRAINT DF_TeamMembers_IsActive DEFAULT 1,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_TeamMembers_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_TeamMembers_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_TeamMembers_IsDeleted DEFAULT 0,

        CONSTRAINT PK_TeamMembers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_TeamMembers_Email UNIQUE NONCLUSTERED (Email),
        CONSTRAINT CK_TeamMembers_Role CHECK (Role IN (N'Owner', N'SalesManager', N'AccountManager'))
    );
END;
GO

-- запит: пошук активних співробітників для призначення відповідального менеджера в угоду
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_TeamMembers_IsDeleted' AND object_id = OBJECT_ID(N'dbo.TeamMembers'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_TeamMembers_IsDeleted
    ON dbo.TeamMembers (IsDeleted)
    INCLUDE (FullName, Email, Role, IsActive)
    WHERE IsDeleted = 0;
END;
GO

-- 3.2. Таблиця Clients (Клієнти / Контрагенти)
IF OBJECT_ID(N'dbo.Clients', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Clients
    (
        -- PK: BIGINT IDENTITY (послідовний автоінкремент, мінімальна фрагментація B-Tree)
        Id BIGINT IDENTITY(1,1) NOT NULL,
        CompanyName NVARCHAR(200) NOT NULL,
        ContactPerson NVARCHAR(150) NOT NULL,
        Email NVARCHAR(255) NOT NULL,
        Phone NVARCHAR(50) NULL,
        Industry NVARCHAR(100) NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Clients_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_Clients_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Clients_IsDeleted DEFAULT 0,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT PK_Clients PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Clients_Email UNIQUE NONCLUSTERED (Email)
    );
END;
GO

-- запит: вибірка активних клієнтів (без м'яко видалених) для селектора та списку контрагентів
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Clients_IsDeleted' AND object_id = OBJECT_ID(N'dbo.Clients'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Clients_IsDeleted
    ON dbo.Clients (IsDeleted)
    INCLUDE (CompanyName, ContactPerson, Email)
    WHERE IsDeleted = 0;
END;
GO

-- 3.3. Таблиця Products (Локальна проекція довідника Прайс-листа)
-- УВАГА: Ідентифікатор без IDENTITY. Жодного міжсерверного FOREIGN KEY!
IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        -- без FK, ідентифікатор з CatalogDb (Product.Id), синхронізується асинхронно
        Id BIGINT NOT NULL, 
        -- копія, оновлюється подіями від власника, джерело: CatalogDb.Product.Name
        Name NVARCHAR(200) NOT NULL,
        -- копія, оновлюється подіями від власника, джерело: CatalogDb.Product.Price
        Price DECIMAL(18,2) NOT NULL,
        -- копія, оновлюється подіями від власника, джерело: CatalogDb.Product.IsActive
        IsActive BIT NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT 1,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Products_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_Products_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,

        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Products_Price CHECK (Price > 0)
    );
END;
GO

-- запит: перевірка наявності, активності та ціни товару при додаванні в угоду (usp_AddDealItem)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Products_IsActive' AND object_id = OBJECT_ID(N'dbo.Products'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Products_IsActive
    ON dbo.Products (IsActive)
    INCLUDE (Name, Price);
END;
GO

-- 3.4. Таблиця Deals (Угоди - Корінь агрегату Deal)
IF OBJECT_ID(N'dbo.Deals', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Deals
    (
        -- PK: BIGINT IDENTITY (послідовний автоінкремент)
        Id BIGINT IDENTITY(1,1) NOT NULL,
        DealNumber NVARCHAR(50) NOT NULL,
        Title NVARCHAR(250) NOT NULL,
        ClientId BIGINT NOT NULL,
        OwnerId BIGINT NOT NULL,
        Stage NVARCHAR(50) NOT NULL CONSTRAINT DF_Deals_Stage DEFAULT N'Lead',
        TotalAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_Deals_TotalAmount DEFAULT 0.00,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Deals_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_Deals_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Deals_IsDeleted DEFAULT 0,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT PK_Deals PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Deals_DealNumber UNIQUE NONCLUSTERED (DealNumber),
        -- 1:N зв'язок Clients -> Deals (FK на боці багатьох, ON DELETE NO ACTION: заборона видалення клієнта з історією угод)
        CONSTRAINT FK_Deals_Clients FOREIGN KEY (ClientId) REFERENCES dbo.Clients(Id) ON DELETE NO ACTION,
        -- 1:N зв'язок TeamMembers -> Deals (FK на боці багатьох, ON DELETE NO ACTION: менеджер з угодами не видаляється)
        CONSTRAINT FK_Deals_TeamMembers FOREIGN KEY (OwnerId) REFERENCES dbo.TeamMembers(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_Deals_Stage CHECK (Stage IN (N'Lead', N'Qualified', N'ProposalSent', N'Negotiation', N'Won', N'Lost')),
        CONSTRAINT CK_Deals_TotalAmount CHECK (TotalAmount >= 0)
    );
END;
GO

-- запит: вибірка всіх угод конкретного клієнта в картці контрагента (WHERE ClientId = @ClientId)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Deals_ClientId' AND object_id = OBJECT_ID(N'dbo.Deals'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Deals_ClientId ON dbo.Deals (ClientId);
END;
GO

-- запит: вибірка угод, закріплених за конкретним менеджером з продажів (WHERE OwnerId = @OwnerId)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Deals_OwnerId' AND object_id = OBJECT_ID(N'dbo.Deals'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Deals_OwnerId ON dbo.Deals (OwnerId);
END;
GO

-- запит: фільтрація угод за етапами воронки продажів для Kanban-дошки (WHERE Stage = @Stage)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Deals_Stage' AND object_id = OBJECT_ID(N'dbo.Deals'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Deals_Stage
    ON dbo.Deals (Stage)
    INCLUDE (DealNumber, Title, ClientId, OwnerId, TotalAmount);
END;
GO

-- запит: вибірка активних угод (WHERE IsDeleted = 0) без сканування архівних/видалених рядків
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Deals_IsDeleted' AND object_id = OBJECT_ID(N'dbo.Deals'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Deals_IsDeleted
    ON dbo.Deals (IsDeleted)
    WHERE IsDeleted = 0;
END;
GO

-- 3.5. Таблиця DealDetails (Деталі угоди: 1:1 зв'язок, PK = FK, ON DELETE CASCADE)
IF OBJECT_ID(N'dbo.DealDetails', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DealDetails
    (
        -- 1:1 зв'язок Deals -> DealDetails: DealId є одночасно PK і FK
        DealId BIGINT NOT NULL,
        Source NVARCHAR(100) NOT NULL CONSTRAINT DF_DealDetails_Source DEFAULT N'Inbound',
        Description NVARCHAR(MAX) NULL,
        LostReason NVARCHAR(500) NULL, -- Обов'язкова при переході в Lost
        ExpectedCloseDate DATETIME2(7) NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_DealDetails_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_DealDetails_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,

        CONSTRAINT PK_DealDetails PRIMARY KEY CLUSTERED (DealId),
        -- 1:1 зв'язок з каскадним видаленням (деталі належать кореню агрегату Deal і видаляються разом з ним)
        CONSTRAINT FK_DealDetails_Deals FOREIGN KEY (DealId) REFERENCES dbo.Deals(Id) ON DELETE CASCADE
    );
END;
GO

-- 3.6. Таблиця DealItems (Позиції угоди: M:N зв'язок, складений PK DealId + ProductId)
IF OBJECT_ID(N'dbo.DealItems', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DealItems
    (
        DealId BIGINT NOT NULL,
        ProductId BIGINT NOT NULL,
        -- знімок, не оновлюється, джерело: CatalogDb.Product.Name (фіксується на момент додавання)
        ProductName NVARCHAR(200) NOT NULL,
        -- знімок, не оновлюється, джерело: CatalogDb.Product.Price (фіксується на момент додавання)
        UnitPrice DECIMAL(18,2) NOT NULL,
        Discount DECIMAL(18,2) NOT NULL CONSTRAINT DF_DealItems_Discount DEFAULT 0.00,
        Quantity INT NOT NULL CONSTRAINT DF_DealItems_Quantity DEFAULT 1,
        TotalPrice AS (Quantity * UnitPrice - Discount) PERSISTED,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_DealItems_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_DealItems_CreatedBy DEFAULT N'system',

        CONSTRAINT PK_DealItems PRIMARY KEY CLUSTERED (DealId, ProductId),
        -- M:N зв'язок Deals <-> Products: FK до Deals з ON DELETE CASCADE (позиції належать агрегату Deal)
        CONSTRAINT FK_DealItems_Deals FOREIGN KEY (DealId) REFERENCES dbo.Deals(Id) ON DELETE CASCADE,
        -- M:N зв'язок Deals <-> Products: FK до локальної Products з ON DELETE NO ACTION (товар з історії не видаляється)
        CONSTRAINT FK_DealItems_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products(Id) ON DELETE NO ACTION,
        CONSTRAINT CK_DealItems_UnitPrice CHECK (UnitPrice > 0),
        CONSTRAINT CK_DealItems_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_DealItems_Discount CHECK (Discount >= 0.00 AND Discount <= (Quantity * UnitPrice))
    );
END;
GO

-- 3.7. Таблиця DealStatusHistory (Журнал переходу статусів воронки продажів)
IF OBJECT_ID(N'dbo.DealStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.DealStatusHistory
    (
        Id BIGINT IDENTITY(1,1) NOT NULL,
        DealId BIGINT NOT NULL,
        OldStatus NVARCHAR(50) NULL,
        NewStatus NVARCHAR(50) NOT NULL,
        ChangedAt DATETIME2(7) NOT NULL CONSTRAINT DF_DealStatusHistory_ChangedAt DEFAULT SYSUTCDATETIME(),
        ChangedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_DealStatusHistory_ChangedBy DEFAULT N'system',
        Reason NVARCHAR(500) NULL,

        CONSTRAINT PK_DealStatusHistory PRIMARY KEY CLUSTERED (Id),
        -- 1:N зв'язок Deals -> DealStatusHistory: каскадне видалення разом з угодою
        CONSTRAINT FK_DealStatusHistory_Deals FOREIGN KEY (DealId) REFERENCES dbo.Deals(Id) ON DELETE CASCADE
    );
END;
GO

-- запит: хронологічний аудит переходів статусів конкретної угоди (WHERE DealId = @DealId ORDER BY ChangedAt)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_DealStatusHistory_DealId_ChangedAt' AND object_id = OBJECT_ID(N'dbo.DealStatusHistory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_DealStatusHistory_DealId_ChangedAt
    ON dbo.DealStatusHistory (DealId, ChangedAt);
END;
GO

-- ============================================================================
-- 4. VIEW vw_DealSummary (Зручне читання угод із клієнтом, власником та сумами)
-- ============================================================================
CREATE OR ALTER VIEW dbo.vw_DealSummary AS
SELECT 
    d.Id AS DealId,
    d.DealNumber,
    d.Title,
    d.Stage,
    d.ClientId,
    c.CompanyName AS ClientCompanyName,
    c.ContactPerson AS ClientContactPerson,
    c.Email AS ClientEmail,
    d.OwnerId,
    tm.FullName AS OwnerName,
    tm.Email AS OwnerEmail,
    dd.Source,
    dd.ExpectedCloseDate,
    dd.LostReason,
    COUNT(di.ProductId) AS ItemsCount,
    ISNULL(SUM(di.TotalPrice), 0.00) AS CalculatedTotal,
    d.TotalAmount AS DealTotalAmount,
    d.CreatedAt,
    d.UpdatedAt
FROM dbo.Deals d
JOIN dbo.Clients c ON d.ClientId = c.Id
JOIN dbo.TeamMembers tm ON d.OwnerId = tm.Id
LEFT JOIN dbo.DealDetails dd ON d.Id = dd.DealId
LEFT JOIN dbo.DealItems di ON d.Id = di.DealId
WHERE d.IsDeleted = 0
GROUP BY 
    d.Id, d.DealNumber, d.Title, d.Stage, d.ClientId, c.CompanyName, c.ContactPerson, c.Email,
    d.OwnerId, tm.FullName, tm.Email, dd.Source, dd.ExpectedCloseDate, dd.LostReason, d.TotalAmount, d.CreatedAt, d.UpdatedAt;
GO
