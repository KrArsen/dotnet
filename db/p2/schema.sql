-- ============================================================================
-- Скрипт: schema.sql (CatalogDb)
-- Призначення: Створення бази даних, схеми, таблиць та індексів під EF Core Code First
-- Контекст: Catalog Bounded Context (Прайс-лист цифрових послуг та товарів)
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Створення бази даних CatalogDb
IF DB_ID(N'CatalogDb') IS NULL
BEGIN
    CREATE DATABASE CatalogDb
    COLLATE Latin1_General_100_CI_AS_SC_UTF8;
END;
GO

USE CatalogDb;
GO

-- ============================================================================
-- 2. ТАБЛИЦІ СХЕМИ КАТАЛОГУ
-- ============================================================================

-- 2.1. Таблиця Category (Категорії послуг та товарів)
IF OBJECT_ID(N'dbo.Category', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Category
    (
        -- PK: BIGINT IDENTITY
        Id BIGINT IDENTITY(1,1) NOT NULL,
        Name NVARCHAR(100) NOT NULL,
        Description NVARCHAR(500) NULL,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Category_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_Category_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Category_IsDeleted DEFAULT 0,

        CONSTRAINT PK_Category PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Category_Name UNIQUE NONCLUSTERED (Name)
    );
END;
GO

-- запит: вибірка активних категорій для випадаючих списків навігації сайту (WHERE IsDeleted = 0)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Category_IsDeleted' AND object_id = OBJECT_ID(N'dbo.Category'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Category_IsDeleted
    ON dbo.Category (IsDeleted)
    WHERE IsDeleted = 0;
END;
GO

-- 2.2. Таблиця Product (Основна сутність позиції прайс-листа)
IF OBJECT_ID(N'dbo.Product', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Product
    (
        -- PK: BIGINT IDENTITY
        Id BIGINT IDENTITY(1,1) NOT NULL,
        Sku NVARCHAR(50) NOT NULL,
        Name NVARCHAR(200) NOT NULL,
        ItemType NVARCHAR(20) NOT NULL CONSTRAINT DF_Product_ItemType DEFAULT N'Service',
        Price DECIMAL(18,2) NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_Product_IsActive DEFAULT 1,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_Product_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_Product_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,
        IsDeleted BIT NOT NULL CONSTRAINT DF_Product_IsDeleted DEFAULT 0,
        RowVersion ROWVERSION NOT NULL,

        CONSTRAINT PK_Product PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT UQ_Product_Sku UNIQUE NONCLUSTERED (Sku),
        CONSTRAINT CK_Product_ItemType CHECK (ItemType IN (N'Service', N'Product')),
        CONSTRAINT CK_Product_Price CHECK (Price > 0)
    );
END;
GO

-- запит: пошук послуги або товару за назвою в каталозі (WHERE Name LIKE @Pattern)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Product_Name' AND object_id = OBJECT_ID(N'dbo.Product'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_Name
    ON dbo.Product (Name);
END;
GO

-- запит: сортування та діапазонна фільтрація прайс-листа за ціною (WHERE Price BETWEEN @Min AND @Max)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Product_Price' AND object_id = OBJECT_ID(N'dbo.Product'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_Price
    ON dbo.Product (Price)
    INCLUDE (Sku, Name, ItemType);
END;
GO

-- запит: фільтрація позицій за типом — послуги чи товари (WHERE ItemType = @Type)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Product_ItemType' AND object_id = OBJECT_ID(N'dbo.Product'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_ItemType
    ON dbo.Product (ItemType);
END;
GO

-- запит: вибірка активних не видалених позицій каталогу (WHERE IsDeleted = 0)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_Product_IsDeleted' AND object_id = OBJECT_ID(N'dbo.Product'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Product_IsDeleted
    ON dbo.Product (IsDeleted)
    WHERE IsDeleted = 0;
END;
GO

-- 2.3. Таблиця ProductDetail (1:1 зв'язок з Product, PK = FK, CASCADE)
IF OBJECT_ID(N'dbo.ProductDetail', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductDetail
    (
        -- 1:1 зв'язок Product -> ProductDetail: ProductId є одночасно PK і FK
        ProductId BIGINT NOT NULL,
        Description NVARCHAR(MAX) NOT NULL,
        Unit NVARCHAR(50) NOT NULL CONSTRAINT DF_ProductDetail_Unit DEFAULT N'послуга',
        DurationMinutes INT NULL, -- Тривалість у хвилинах (NULL для товарів)
        TaxRate DECIMAL(5,2) NOT NULL CONSTRAINT DF_ProductDetail_TaxRate DEFAULT 20.00,
        CreatedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ProductDetail_CreatedAt DEFAULT SYSUTCDATETIME(),
        CreatedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_ProductDetail_CreatedBy DEFAULT N'system',
        UpdatedAt DATETIME2(7) NULL,
        UpdatedBy NVARCHAR(100) NULL,

        CONSTRAINT PK_ProductDetail PRIMARY KEY CLUSTERED (ProductId),
        -- 1:1 зв'язок Product -> ProductDetail: ON DELETE CASCADE
        CONSTRAINT FK_ProductDetail_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id) ON DELETE CASCADE
    );
END;
GO

-- 2.4. Таблиця PriceHistory (1:N зв'язок з Product: історія тарифів)
IF OBJECT_ID(N'dbo.PriceHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.PriceHistory
    (
        -- PK: BIGINT IDENTITY
        Id BIGINT IDENTITY(1,1) NOT NULL,
        ProductId BIGINT NOT NULL,
        Price DECIMAL(18,2) NOT NULL,
        ValidFrom DATETIME2(7) NOT NULL CONSTRAINT DF_PriceHistory_ValidFrom DEFAULT SYSUTCDATETIME(),
        ValidTo DATETIME2(7) NULL, -- NULL означає діючий тариф
        ChangedBy NVARCHAR(100) NOT NULL CONSTRAINT DF_PriceHistory_ChangedBy DEFAULT N'system',
        Reason NVARCHAR(250) NULL,

        CONSTRAINT PK_PriceHistory PRIMARY KEY CLUSTERED (Id),
        -- 1:N зв'язок Product -> PriceHistory: ON DELETE CASCADE (історія видаляється разом з товаром)
        CONSTRAINT FK_PriceHistory_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id) ON DELETE CASCADE,
        CONSTRAINT CK_PriceHistory_Price CHECK (Price > 0),
        CONSTRAINT CK_PriceHistory_ValidTo CHECK (ValidTo IS NULL OR ValidTo > ValidFrom)
    );
END;
GO

-- запит: швидке отримання хронології цін конкретної послуги (WHERE ProductId = @Id ORDER BY ValidFrom DESC)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_PriceHistory_ProductId_ValidFrom' AND object_id = OBJECT_ID(N'dbo.PriceHistory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_PriceHistory_ProductId_ValidFrom
    ON dbo.PriceHistory (ProductId, ValidFrom DESC);
END;
GO

-- 2.5. Таблиця ProductCategory (M:N зв'язок з власним полем AddedAt)
IF OBJECT_ID(N'dbo.ProductCategory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.ProductCategory
    (
        ProductId BIGINT NOT NULL,
        CategoryId BIGINT NOT NULL,
        AddedAt DATETIME2(7) NOT NULL CONSTRAINT DF_ProductCategory_AddedAt DEFAULT SYSUTCDATETIME(),

        CONSTRAINT PK_ProductCategory PRIMARY KEY CLUSTERED (ProductId, CategoryId),
        -- M:N зв'язок Product <-> Category: FK до Product (ON DELETE CASCADE)
        CONSTRAINT FK_ProductCategory_Product FOREIGN KEY (ProductId) REFERENCES dbo.Product(Id) ON DELETE CASCADE,
        -- M:N зв'язок Product <-> Category: FK до Category (ON DELETE CASCADE)
        CONSTRAINT FK_ProductCategory_Category FOREIGN KEY (CategoryId) REFERENCES dbo.Category(Id) ON DELETE CASCADE
    );
END;
GO

-- запит: швидка вибірка всіх товарів певної категорії (WHERE CategoryId = @CategoryId)
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_ProductCategory_CategoryId' AND object_id = OBJECT_ID(N'dbo.ProductCategory'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ProductCategory_CategoryId
    ON dbo.ProductCategory (CategoryId);
END;
GO
