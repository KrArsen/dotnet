-- ============================================================================
-- Скрипт: security.sql (SalesDb)
-- Призначення: Налаштування логіна та користувача найменших привілеїв (Least Privilege)
-- Контекст: Sales Bounded Context
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE SalesDb;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- 1. Створення серверного логіна для сервісу продажів
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'sales_user')
BEGIN
    CREATE LOGIN sales_user 
    WITH PASSWORD = N'SalesDbSecureP@ssw0rd2026!', 
         CHECK_POLICY = OFF;
END;
GO

-- 2. Створення користувача в базі даних SalesDb
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'sales_user')
BEGIN
    CREATE USER sales_user FOR LOGIN sales_user;
END;
GO

-- 3. Надання прав найменших привілеїв:
--    - Дозвіл на читання та запис у таблиці застосунку
--    - Дозвіл на виконання збережених процедур
--    - ЗАБОРОНА прав DDL, ALTER DATABASE, перегляду системних об'єктів
ALTER ROLE db_datareader ADD MEMBER sales_user;
ALTER ROLE db_datawriter ADD MEMBER sales_user;
GRANT EXECUTE TO sales_user;
GRANT SELECT ON dbo.vw_DealSummary TO sales_user;
GO

PRINT N'[SECURITY] Користувача sales_user успішно налаштовано за принципом найменших привілеїв.';
GO
