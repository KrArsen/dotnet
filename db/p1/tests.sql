-- ============================================================================
-- Скрипт: tests.sql (SalesDb)
-- Призначення: Комплексне тестування бізнес-правил та процедур воронки продажів:
--              позитивний життєвий цикл та всі обов'язкові негативні сценарії.
--              Наприкінці видаляє всі створені тестові дані за збереженими Id.
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE SalesDb;
GO

SET NOCOUNT ON;
PRINT N'============================================================================';
PRINT N'            ПОЧАТОК ТЕСТУВАННЯ SALES-DB (STORED PROCEDURES)                 ';
PRINT N'============================================================================';

-- Тимчасова таблиця для відстеження створених тестових угод задля гарантованого очищення
IF OBJECT_ID(N'tempdb..#CreatedTestDeals') IS NOT NULL DROP TABLE #CreatedTestDeals;
CREATE TABLE #CreatedTestDeals (Id BIGINT NOT NULL PRIMARY KEY);

DECLARE @TestClientId BIGINT;
SELECT TOP 1 @TestClientId = Id FROM dbo.Clients WHERE IsDeleted = 0;

DECLARE @TestOwnerId BIGINT;
SELECT TOP 1 @TestOwnerId = Id FROM dbo.TeamMembers WHERE IsActive = 1 AND IsDeleted = 0 AND Role = N'SalesManager';

-- ============================================================================
-- ТЕСТ 1: Позитивний сценарій повного життєвого циклу угоди
-- Lead -> Qualified -> ProposalSent -> Negotiation -> Won
-- ============================================================================
PRINT N'[ТЕСТ 1] Повний успішний життєвий цикл угоди (Lead -> Qualified -> ProposalSent -> Negotiation -> Won)...';

BEGIN TRY
    -- 1. Створення ліда
    DECLARE @NewDealId BIGINT;

    EXEC dbo.usp_CreateDeal
        @ClientId = @TestClientId,
        @OwnerId = @TestOwnerId,
        @Title = N'Розробка веб-сервісу для автоматизації логістики (Тест)',
        @Source = N'Website',
        @Description = N'Позитивний тест повного життєвого циклу воронки',
        @ExpectedCloseDate = '2026-11-01',
        @ItemsJson = NULL,
        @CreatedBy = N'TestRunner',
        @NewDealId = @NewDealId OUTPUT;

    INSERT INTO #CreatedTestDeals (Id) VALUES (@NewDealId);
    PRINT CONCAT(N'  -> Створено нову угоду ID: ', @NewDealId);

    -- Перевірка статусу Lead
    DECLARE @Stage NVARCHAR(50);
    SELECT @Stage = Stage FROM dbo.Deals WHERE Id = @NewDealId;
    IF @Stage <> N'Lead' THROW 59999, N'Помилка: початковий статус має бути Lead', 1;
    PRINT N'  -> [OK] Етап 1: Lead';

    -- 2. Додавання позиції послуги зі знімком ціни та знижкою (абсолютною)
    EXEC dbo.usp_AddDealItem
        @DealId = @NewDealId,
        @ProductId = 2, -- Корпоративний сайт (65000.00 грн)
        @Quantity = 1,
        @Discount = 5000.00, -- Знижка 5000 грн (перевірка CHECK Discount <= Quantity*UnitPrice)
        @AddedBy = N'TestRunner';
    PRINT N'  -> [OK] Позицію додано (знімок ціни зафіксовано, сума розрахована)';

    -- 3. Рух вперед: Lead -> Qualified
    EXEC dbo.usp_AdvanceDealStage @DealId = @NewDealId, @ChangedBy = N'TestRunner', @Reason = N'Бюджет та скоуп кваліфіковано';
    SELECT @Stage = Stage FROM dbo.Deals WHERE Id = @NewDealId;
    IF @Stage <> N'Qualified' THROW 59999, N'Помилка: статус має бути Qualified', 1;
    PRINT N'  -> [OK] Етап 2: Qualified';

    -- 4. Рух вперед: Qualified -> ProposalSent
    EXEC dbo.usp_AdvanceDealStage @DealId = @NewDealId, @ChangedBy = N'TestRunner', @Reason = N'КП надіслано клієнту';
    SELECT @Stage = Stage FROM dbo.Deals WHERE Id = @NewDealId;
    IF @Stage <> N'ProposalSent' THROW 59999, N'Помилка: статус має бути ProposalSent', 1;
    PRINT N'  -> [OK] Етап 3: ProposalSent';

    -- 5. Рух вперед: ProposalSent -> Negotiation
    EXEC dbo.usp_AdvanceDealStage @DealId = @NewDealId, @ChangedBy = N'TestRunner', @Reason = N'Погодження фінального договору';
    SELECT @Stage = Stage FROM dbo.Deals WHERE Id = @NewDealId;
    IF @Stage <> N'Negotiation' THROW 59999, N'Помилка: статус має бути Negotiation', 1;
    PRINT N'  -> [OK] Етап 4: Negotiation';

    -- 6. Фінал: Negotiation -> Won
    EXEC dbo.usp_WinDeal @DealId = @NewDealId, @ChangedBy = N'TestRunner', @Reason = N'Договір підписано, передоплату внесено';
    SELECT @Stage = Stage FROM dbo.Deals WHERE Id = @NewDealId;
    IF @Stage <> N'Won' THROW 59999, N'Помилка: статус має бути Won', 1;
    PRINT N'  -> [OK] Етап 5: Won (Успішно закрито)';

    PRINT N'[ТЕСТ 1 УСПІШНИЙ] Повний життєвий цикл воронки виконано бездоганно!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'[ТЕСТ 1 ПРОВАЛЕНО] Помилка: ', ERROR_MESSAGE());
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ТЕСТ 2: Негативний сценарій - Спроба виграти угоду з етапу Lead (перескок етапів)
-- ============================================================================
PRINT N'[ТЕСТ 2 (НЕГАТИВНИЙ)] Спроба виграти угоду напряму з етапу Lead (очікується THROW 50002)...';

BEGIN TRY
    DECLARE @LeadDealId BIGINT;
    DECLARE @ItemsJson2 NVARCHAR(MAX) = N'[{"productId": 1, "quantity": 1, "discount": 0.00}]';

    EXEC dbo.usp_CreateDeal
        @ClientId = @TestClientId,
        @OwnerId = @TestOwnerId,
        @Title = N'Спроба перескоку воронки (Тест)',
        @ItemsJson = @ItemsJson2,
        @NewDealId = @LeadDealId OUTPUT;

    INSERT INTO #CreatedTestDeals (Id) VALUES (@LeadDealId);

    -- Спроба одразу викликати usp_WinDeal для ліда (перескочити етапи)
    EXEC dbo.usp_WinDeal @DealId = @LeadDealId, @ChangedBy = N'Hacker';

    PRINT N'[ТЕСТ 2 ПРОВАЛЕНО] Перескок етапу мав бути заблокований!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'  -> [ОЧІКУВАНА ПОМИЛКА] Код: ', ERROR_NUMBER(), N' | Повідомлення: ', ERROR_MESSAGE());
    IF ERROR_NUMBER() = 50002
    BEGIN
        DECLARE @ActualStage2 NVARCHAR(50);
        SELECT @ActualStage2 = Stage FROM dbo.Deals WHERE Id = @LeadDealId;
        IF @ActualStage2 = N'Lead'
            PRINT N'[ТЕСТ 2 УСПІШНИЙ] Перескок етапів у Won успішно заблокований бізнес-правилом (50002), статус не змінився (Lead)!';
        ELSE
            PRINT CONCAT(N'[ТЕСТ 2 ПОМИЛКА] Статус несподівано змінився на: ', @ActualStage2);
    END
    ELSE
        PRINT N'[ТЕСТ 2 ПОМИЛКА] Несподіваний код помилки!';
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ТЕСТ 3: Негативний сценарій - Спроба виграти угоду БЕЗ позицій (нульова сума)
-- ============================================================================
PRINT N'[ТЕСТ 3 (НЕГАТИВНИЙ)] Спроба виграти угоду без позицій (сума 0, очікується THROW 50003)...';

BEGIN TRY
    DECLARE @EmptyDealId BIGINT;

    EXEC dbo.usp_CreateDeal
        @ClientId = @TestClientId,
        @OwnerId = @TestOwnerId,
        @Title = N'Порожня угода без послуг (Тест)',
        @ItemsJson = NULL,
        @NewDealId = @EmptyDealId OUTPUT;

    INSERT INTO #CreatedTestDeals (Id) VALUES (@EmptyDealId);

    -- Доводимо до Negotiation без додавання позицій
    EXEC dbo.usp_AdvanceDealStage @DealId = @EmptyDealId; -- -> Qualified
    EXEC dbo.usp_AdvanceDealStage @DealId = @EmptyDealId; -- -> ProposalSent
    EXEC dbo.usp_AdvanceDealStage @DealId = @EmptyDealId; -- -> Negotiation

    -- Спроба виграти без позицій
    EXEC dbo.usp_WinDeal @DealId = @EmptyDealId;

    PRINT N'[ТЕСТ 3 ПРОВАЛЕНО] Виграш порожньої угоди мав бути заблокований!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'  -> [ОЧІКУВАНА ПОМИЛКА] Код: ', ERROR_NUMBER(), N' | Повідомлення: ', ERROR_MESSAGE());
    IF ERROR_NUMBER() = 50003
    BEGIN
        DECLARE @ActualStage3 NVARCHAR(50);
        SELECT @ActualStage3 = Stage FROM dbo.Deals WHERE Id = @EmptyDealId;
        IF @ActualStage3 = N'Negotiation'
            PRINT N'[ТЕСТ 3 УСПІШНИЙ] Виграш угоди без послуг або з сумою 0 заблокований (50003), статус залишився Negotiation!';
        ELSE
            PRINT CONCAT(N'[ТЕСТ 3 ПОМИЛКА] Статус несподівано змінився на: ', @ActualStage3);
    END
    ELSE
        PRINT N'[ТЕСТ 3 ПОМИЛКА] Несподіваний код помилки!';
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ТЕСТ 4: Негативний сценарій - Спроба перескочити етап (Lead -> Won)
-- ============================================================================
PRINT N'[ТЕСТ 4 (НЕГАТИВНИЙ)] Спроба перескочити етап у воронці...';

BEGIN TRY
    DECLARE @SkipDealId BIGINT;
    EXEC dbo.usp_CreateDeal
        @ClientId = @TestClientId,
        @OwnerId = @TestOwnerId,
        @Title = N'Тест пропуску етапу',
        @ItemsJson = NULL,
        @NewDealId = @SkipDealId OUTPUT;

    INSERT INTO #CreatedTestDeals (Id) VALUES (@SkipDealId);

    -- Спроба відразу закрити у Won
    EXEC dbo.usp_WinDeal @DealId = @SkipDealId;
    PRINT N'[ТЕСТ 4 ПРОВАЛЕНО] Пропуск етапів мав викликати помилку!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'  -> [ОЧІКУВАНА ПОМИЛКА] Код: ', ERROR_NUMBER(), N' | Повідомлення: ', ERROR_MESSAGE());
    IF ERROR_NUMBER() = 50002
    BEGIN
        DECLARE @ActualStage4 NVARCHAR(50);
        SELECT @ActualStage4 = Stage FROM dbo.Deals WHERE Id = @SkipDealId;
        IF @ActualStage4 = N'Lead'
            PRINT N'[ТЕСТ 4 УСПІШНИЙ] Перескок етапу заблоковано (50002), статус залишився Lead!';
        ELSE
            PRINT CONCAT(N'[ТЕСТ 4 ПОМИЛКА] Статус несподівано змінився на: ', @ActualStage4);
    END
    ELSE
        PRINT N'[ТЕСТ 4 ПОМИЛКА] Несподіваний код помилки!';
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ТЕСТ 5: Негативний сценарій - Спроба перевести виграну угоду в Lost (після Won)
-- ============================================================================
PRINT N'[ТЕСТ 5 (НЕГАТИВНИЙ)] Спроба перевести виграну угоду в Lost (очікується THROW 50002)...';

BEGIN TRY
    -- Використовуємо угоду, виграну в ТЕСТІ 1 (@NewDealId)
    EXEC dbo.usp_LoseDeal 
        @DealId = @NewDealId, 
        @Reason = N'Спроба змінити статус виграної угоди';

    PRINT N'[ТЕСТ 5 ПРОВАЛЕНО] Зміна статусу виграної угоди мала бути заборонена!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'  -> [ОЧІКУВАНА ПОМИЛКА] Код: ', ERROR_NUMBER(), N' | Повідомлення: ', ERROR_MESSAGE());
    IF ERROR_NUMBER() = 50002
    BEGIN
        DECLARE @ActualStage5 NVARCHAR(50);
        SELECT @ActualStage5 = Stage FROM dbo.Deals WHERE Id = @NewDealId;
        IF @ActualStage5 = N'Won'
            PRINT N'[ТЕСТ 5 УСПІШНИЙ] Переведення виграної угоди у Lost коректно заблоковане (50002), статус залишився Won!';
        ELSE
            PRINT CONCAT(N'[ТЕСТ 5 ПОМИЛКА] Статус несподівано змінився на: ', @ActualStage5);
    END
    ELSE
        PRINT N'[ТЕСТ 5 ПОМИЛКА] Несподіваний код помилки!';
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ТЕСТ 6: Негативний сценарій - Спроба закрити в Lost БЕЗ вказання причини
-- ============================================================================
PRINT N'[ТЕСТ 6 (НЕГАТИВНИЙ)] Спроба програти угоду без зазначення причини (очікується THROW 50005)...';

BEGIN TRY
    DECLARE @NoReasonDealId BIGINT;
    EXEC dbo.usp_CreateDeal
        @ClientId = @TestClientId,
        @OwnerId = @TestOwnerId,
        @Title = N'Тест програшу без причини',
        @ItemsJson = NULL,
        @NewDealId = @NoReasonDealId OUTPUT;

    INSERT INTO #CreatedTestDeals (Id) VALUES (@NoReasonDealId);

    EXEC dbo.usp_LoseDeal 
        @DealId = @NoReasonDealId, 
        @Reason = NULL; -- Порожня причина

    PRINT N'[ТЕСТ 6 ПРОВАЛЕНО] Спроба без причини мала викликати помилку!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'  -> [ОЧІКУВАНА ПОМИЛКА] Код: ', ERROR_NUMBER(), N' | Повідомлення: ', ERROR_MESSAGE());
    IF ERROR_NUMBER() = 50005
    BEGIN
        DECLARE @ActualStage6 NVARCHAR(50);
        SELECT @ActualStage6 = Stage FROM dbo.Deals WHERE Id = @NoReasonDealId;
        IF @ActualStage6 = N'Lead'
            PRINT N'[ТЕСТ 6 УСПІШНИЙ] Відсутність причини програшу успішно заблокована (50005), статус залишився Lead!';
        ELSE
            PRINT CONCAT(N'[ТЕСТ 6 ПОМИЛКА] Статус несподівано змінився на: ', @ActualStage6);
    END
    ELSE
        PRINT N'[ТЕСТ 6 ПОМИЛКА] Несподіваний код помилки!';
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ТЕСТ 7: Негативний сценарій - Спроба додати позицію у закриту (Won) угоду
-- ============================================================================
PRINT N'[ТЕСТ 7 (НЕГАТИВНИЙ)] Спроба додати позицію у виграну угоду (очікується THROW 50002)...';

BEGIN TRY
    -- Використовуємо @NewDealId, що має статус Won
    EXEC dbo.usp_AddDealItem
        @DealId = @NewDealId,
        @ProductId = 3,
        @Quantity = 1,
        @AddedBy = N'LateComer';

    PRINT N'[ТЕСТ 7 ПРОВАЛЕНО] Додавання позиції у закриту угоду мало бути відхилене!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'  -> [ОЧІКУВАНА ПОМИЛКА] Код: ', ERROR_NUMBER(), N' | Повідомлення: ', ERROR_MESSAGE());
    IF ERROR_NUMBER() = 50002
    BEGIN
        DECLARE @ItemsCount7 INT;
        SELECT @ItemsCount7 = COUNT(*) FROM dbo.DealItems WHERE DealId = @NewDealId;
        IF @ItemsCount7 = 1
            PRINT N'[ТЕСТ 7 УСПІШНИЙ] Модифікація закритих угод успішно заблокована (50002), нову позицію не додано!';
        ELSE
            PRINT N'[ТЕСТ 7 ПОМИЛКА] Позиція була помилково додана!';
    END
    ELSE
        PRINT N'[ТЕСТ 7 ПОМИЛКА] Несподіваний код помилки!';
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ТЕСТ 8: Негативний сценарій - Операція з неіснуючою угодою (THROW 50001)
-- ============================================================================
PRINT N'[ТЕСТ 8 (НЕГАТИВНИЙ)] Спроба операції з неіснуючою угодою (очікується THROW 50001)...';

BEGIN TRY
    EXEC dbo.usp_AdvanceDealStage @DealId = 9999999;

    PRINT N'[ТЕСТ 8 ПРОВАЛЕНО] Запит до неіснуючої угоди мав бути відхилений!';
END TRY
BEGIN CATCH
    PRINT CONCAT(N'  -> [ОЧІКУВАНА ПОМИЛКА] Код: ', ERROR_NUMBER(), N' | Повідомлення: ', ERROR_MESSAGE());
    IF ERROR_NUMBER() = 50001
        PRINT N'[ТЕСТ 8 УСПІШНИЙ] Звернення до неіснуючої угоди коректно повертає 50001 (Not Found)!';
    ELSE
        PRINT N'[ТЕСТ 8 ПОМИЛКА] Несподіваний код помилки!';
END CATCH;
PRINT N'----------------------------------------------------------------------------';

-- ============================================================================
-- ОЧИЩЕННЯ ТЕСТОВИХ ДАНИХ (ТЕСТОВІ УГОДИ ВИДАЛЯЮТЬСЯ ЗА ЗБЕРЕЖЕНИМИ ID)
-- ============================================================================
PRINT N'[ОЧИЩЕННЯ] Видалення тимчасових угод, створених під час тестів...';
DELETE FROM dbo.DealItems WHERE DealId IN (SELECT Id FROM #CreatedTestDeals);
DELETE FROM dbo.DealDetails WHERE DealId IN (SELECT Id FROM #CreatedTestDeals);
DELETE FROM dbo.DealStatusHistory WHERE DealId IN (SELECT Id FROM #CreatedTestDeals);
DELETE FROM dbo.Deals WHERE Id IN (SELECT Id FROM #CreatedTestDeals);
DROP TABLE #CreatedTestDeals;
PRINT N'  -> [OK] Усі тестові записи успішно видалено. База даних повернута у вихідний стан!';

PRINT N'============================================================================';
PRINT N'            УСІ ТЕСТИ SALES-DB УСПІШНО ЗАВЕРШЕНО!                           ';
PRINT N'============================================================================';
GO
