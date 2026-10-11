-- ============================================================================
-- Скрипт: security.sql (CatalogDb)
-- Призначення: Налаштування логіна та користувача найменших привілеїв (Least Privilege)
-- Контекст: Catalog Bounded Context
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE CatalogDb;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Створення серверного логіна для сервісу каталогу
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'catalog_user')
BEGIN
    CREATE LOGIN catalog_user 
    WITH PASSWORD = N'CatalogDbSecureP@ssw0rd2026!', 
         CHECK_POLICY = OFF;
END;
GO

-- 2. Створення користувача в базі даних CatalogDb
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'catalog_user')
BEGIN
    CREATE USER catalog_user FOR LOGIN catalog_user;
END;
GO

-- 3. Надання прав найменших привілеїв (читання та запис у таблиці)
ALTER ROLE db_datareader ADD MEMBER catalog_user;
ALTER ROLE db_datawriter ADD MEMBER catalog_user;
GO

PRINT N'[SECURITY] Користувача catalog_user успішно налаштовано за принципом найменших привілеїв.';
GO
