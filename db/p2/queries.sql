-- ============================================================================
-- Скрипт: queries.sql (CatalogDb)
-- Призначення: Типові запити сервісу прайс-листа (пошук послуг, детальне КП,
--              історія цін, пагінація та перевірка ізоляції зовнішніх ключів)
-- Контекст: Catalog Bounded Context
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE CatalogDb;
GO

SET NOCOUNT ON;
GO

-- ============================================================================
-- 1. Отримання повної інформації про послугу (Product + Detail + Categories + Active Price)
-- Аналог EF Core: context.Products.Include(p => p.Detail).Include(p => p.Categories)...
-- ============================================================================
DECLARE @TargetProductId BIGINT = 1;

SELECT 
    p.Id AS ProductId,
    p.Sku,
    p.Name AS ProductName,
    p.ItemType,
    p.Price AS CurrentPrice,
    pd.Description AS FullDescription,
    pd.Unit,
    pd.DurationMinutes,
    pd.TaxRate,
    STRING_AGG(c.Name, N', ') AS Categories
FROM dbo.Product p
LEFT JOIN dbo.ProductDetail pd ON p.Id = pd.ProductId
LEFT JOIN dbo.ProductCategory pc ON p.Id = pc.ProductId
LEFT JOIN dbo.Category c ON pc.CategoryId = c.Id
WHERE p.Id = @TargetProductId AND p.IsDeleted = 0
GROUP BY 
    p.Id, p.Sku, p.Name, p.ItemType, p.Price, 
    pd.Description, pd.Unit, pd.DurationMinutes, pd.TaxRate;

-- ============================================================================
-- 2. Фільтрація послуг прайс-листа за категорією та діапазоном цін з пагінацією
-- ============================================================================
DECLARE @CategoryName NVARCHAR(100) = N'Веб-розробка';
DECLARE @MinPrice DECIMAL(18,2) = 10000.00;
DECLARE @MaxPrice DECIMAL(18,2) = 80000.00;
DECLARE @PageNumber INT = 1;
DECLARE @PageSize INT = 5;

SELECT 
    p.Id,
    p.Sku,
    p.Name,
    p.ItemType,
    p.Price,
    pd.Unit,
    c.Name AS Category
FROM dbo.Product p
JOIN dbo.ProductCategory pc ON p.Id = pc.ProductId
JOIN dbo.Category c ON pc.CategoryId = c.Id
JOIN dbo.ProductDetail pd ON p.Id = pd.ProductId
WHERE p.IsDeleted = 0
  AND c.Name = @CategoryName
  AND p.Price BETWEEN @MinPrice AND @MaxPrice
ORDER BY p.Price DESC
OFFSET (@PageNumber - 1) * @PageSize ROWS
FETCH NEXT @PageSize ROWS ONLY;

-- ============================================================================
-- 3. Перегляд хронології зміни тарифів (PriceHistory) для послуги
-- ============================================================================
SELECT 
    ph.Id,
    ph.ProductId,
    p.Name AS ProductName,
    ph.Price,
    ph.ValidFrom,
    ph.ValidTo,
    ph.Reason
FROM dbo.PriceHistory ph
JOIN dbo.Product p ON ph.ProductId = p.Id
WHERE ph.ProductId = 1
ORDER BY ph.ValidFrom DESC;

-- ============================================================================
-- 4. Перевірка відсутності міжсерверних зовнішніх ключів (Self-Check #3)
-- Запит повинен повернути виключно внутрішні FK CatalogDb
-- ============================================================================
SELECT 
    fk.name AS ForeignKeyName,
    OBJECT_NAME(fk.parent_object_id) AS TableName,
    COL_NAME(fkc.parent_object_id, fkc.parent_column_id) AS ColumnName,
    OBJECT_NAME(fk.referenced_object_id) AS ReferencedTable,
    COL_NAME(fkc.referenced_object_id, fkc.referenced_column_id) AS ReferencedColumn
FROM sys.foreign_keys fk
JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
ORDER BY TableName;
GO
