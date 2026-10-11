-- ============================================================================
-- Скрипт: procedures.sql (SalesDb)
-- Призначення: 6 збережених транзакційних процедур управління угодами воронки
-- Контекст: Sales Bounded Context (CRM для малого бізнесу)
-- СУБД: Microsoft SQL Server 2022
-- ============================================================================

USE SalesDb;
GO

SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

-- ============================================================================
-- 1. usp_CreateDeal
-- Призначення: Транзакційне створення нової угоди (початковий етап Lead), збереження
--              деталей угоди (1:1), фіксація знімків цін позицій з локальної Products (1:N)
--              та початкового запису в аудит-журналі воронки.
-- Параметри:
--   @ClientId BIGINT - ID контрагента (має існувати та не бути видаленим)
--   @OwnerId BIGINT - ID відповідального менеджера (активний співробітник)
--   @Title NVARCHAR(250) - Назва угоди
--   @Source NVARCHAR(100) - Джерело ліда (Website, Referral тощо)
--   @Description NVARCHAR(MAX) - Опис угоди
--   @ExpectedCloseDate DATETIME2(7) - Очікувана дата завершення
--   @ItemsJson NVARCHAR(MAX) - JSON-масив позицій [{"productId":1, "quantity":1, "discount":0}]
--   @CreatedBy NVARCHAR(100) - Автор операції
--   @DealNumber NVARCHAR(50) - Номер угоди (якщо NULL, генерується автоматично)
--   @NewDealId BIGINT OUTPUT - Ідентифікатор створеної угоди
-- Допустимі переходи: Створення угоди виключно у статусі Lead.
-- Коди помилок (THROW):
--   50001: Клієнта або відповідального менеджера не знайдено чи деактивовано.
--   50004: Невалідний JSON або товар/послугу не знайдено/деактивовано в локальній репліці.
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.usp_CreateDeal
    @ClientId BIGINT,
    @OwnerId BIGINT,
    @Title NVARCHAR(250),
    @Source NVARCHAR(100) = N'Inbound',
    @Description NVARCHAR(MAX) = NULL,
    @ExpectedCloseDate DATETIME2(7) = NULL,
    @ItemsJson NVARCHAR(MAX) = NULL,
    @CreatedBy NVARCHAR(100) = N'SalesSystem',
    @DealNumber NVARCHAR(50) = NULL,
    @NewDealId BIGINT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- 1. Валідація існування та активності клієнта
    IF NOT EXISTS (SELECT 1 FROM dbo.Clients WHERE Id = @ClientId AND IsDeleted = 0)
    BEGIN
        THROW 50001, N'Client not found or deleted', 1;
    END;

    -- 2. Валідація існування та активності відповідального співробітника
    IF NOT EXISTS (SELECT 1 FROM dbo.TeamMembers WHERE Id = @OwnerId AND IsActive = 1 AND IsDeleted = 0)
    BEGIN
        THROW 50001, N'Owner team member not found or inactive', 1;
    END;

    -- 3. Валідація позицій (якщо передані у JSON): товари мають бути в локальній копії Products та активні
    IF @ItemsJson IS NOT NULL AND LTRIM(RTRIM(@ItemsJson)) <> N''
    BEGIN
        IF ISJSON(@ItemsJson) <> 1
        BEGIN
            THROW 50004, N'Invalid JSON format in @ItemsJson', 1;
        END;

        -- Перевіряємо наявність та активність кожного переданого товару
        IF EXISTS (
            SELECT 1 
            FROM OPENJSON(@ItemsJson) 
            WITH (
                ProductId BIGINT '$.productId',
                Quantity INT '$.quantity',
                Discount DECIMAL(18,2) '$.discount'
            ) j
            LEFT JOIN dbo.Products p WITH (UPDLOCK, ROWLOCK) ON j.ProductId = p.Id AND p.IsActive = 1
            WHERE p.Id IS NULL
        )
        BEGIN
            THROW 50004, N'One or more products not found or inactive in catalog copy', 1;
        END;
    END;

    -- 4. Генерація номера угоди за замовчуванням
    IF @DealNumber IS NULL OR LTRIM(RTRIM(@DealNumber)) = N''
    BEGIN
        SET @DealNumber = CONCAT(N'DL-', FORMAT(SYSUTCDATETIME(), N'yyyyMMdd'), N'-', CAST(FLOOR(RAND() * 8999 + 1000) AS INT));
    END;

    -- 5. Створення запису в Deals з початковим етапом Lead
    INSERT INTO dbo.Deals (DealNumber, Title, ClientId, OwnerId, Stage, TotalAmount, CreatedAt, CreatedBy, IsDeleted)
    VALUES (@DealNumber, @Title, @ClientId, @OwnerId, N'Lead', 0.00, SYSUTCDATETIME(), @CreatedBy, 0);

    SET @NewDealId = SCOPE_IDENTITY();

    -- 6. Створення деталей угоди (DealDetails - зв'язок 1:1)
    INSERT INTO dbo.DealDetails (DealId, Source, Description, LostReason, ExpectedCloseDate, CreatedAt, CreatedBy)
    VALUES (@NewDealId, @Source, @Description, NULL, @ExpectedCloseDate, SYSUTCDATETIME(), @CreatedBy);

    -- 7. Додавання позицій зі ЗНІМКОМ (Snapshot) назви та вартості
    IF @ItemsJson IS NOT NULL AND LTRIM(RTRIM(@ItemsJson)) <> N''
    BEGIN
        INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity, CreatedAt, CreatedBy)
        SELECT 
            @NewDealId, 
            p.Id, 
            p.Name, 
            p.Price, 
            ISNULL(j.Discount, 0.00), 
            ISNULL(j.Quantity, 1), 
            SYSUTCDATETIME(), 
            @CreatedBy
        FROM OPENJSON(@ItemsJson)
        WITH (
            ProductId BIGINT '$.productId',
            Quantity INT '$.quantity',
            Discount DECIMAL(18,2) '$.discount'
        ) j
        JOIN dbo.Products p ON j.ProductId = p.Id;

        -- Перерахунок суми угоди
        UPDATE d
        SET d.TotalAmount = ISNULL((SELECT SUM(TotalPrice) FROM dbo.DealItems WHERE DealId = @NewDealId), 0.00),
            d.UpdatedAt = SYSUTCDATETIME(),
            d.UpdatedBy = @CreatedBy
        FROM dbo.Deals d
        WHERE d.Id = @NewDealId;
    END;

    -- 8. Перший запис в історію статусів
    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES (@NewDealId, NULL, N'Lead', SYSUTCDATETIME(), @CreatedBy, N'Створення нового ліда / реєстрація угоди');

    COMMIT TRANSACTION;
END;
GO

-- ============================================================================
-- 2. usp_GetDealById
-- Призначення: Отримання повної інформації про угоду через 3 результуючі набори 
--              (Multi-Mapping для Dapper QueryMultiple / ADO.NET):
--              Набір 1: Загальні дані угоди, клієнта, менеджера та деталі (DealDetails);
--              Набір 2: Позиції угоди з незмінними знімками цін (DealItems);
--              Набір 3: Хронологічний аудит переходів воронки (DealStatusHistory).
-- Параметри:
--   @DealId BIGINT - Ідентифікатор угоди
-- Допустимі переходи: Операція читання (без змін стану).
-- Коди помилок (THROW):
--   50001: Угоду не знайдено або її м'яко видалено (IsDeleted = 1).
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.usp_GetDealById
    @DealId BIGINT
AS
BEGIN
    SET NOCOUNT ON;

    IF NOT EXISTS (SELECT 1 FROM dbo.Deals WHERE Id = @DealId AND IsDeleted = 0)
    BEGIN
        THROW 50001, N'Угоду не знайдено або її видалено.', 1;
    END;

    -- Набір 1: Загальна інформація про угоду, клієнта, менеджера та деталі
    SELECT 
        d.Id AS DealId,
        d.DealNumber,
        d.Title,
        d.Stage,
        d.TotalAmount,
        d.CreatedAt,
        d.UpdatedAt,
        c.Id AS ClientId,
        c.CompanyName,
        c.ContactPerson,
        c.Email AS ClientEmail,
        c.Phone AS ClientPhone,
        tm.Id AS OwnerId,
        tm.FullName AS OwnerName,
        tm.Email AS OwnerEmail,
        dd.Source,
        dd.Description,
        dd.LostReason,
        dd.ExpectedCloseDate
    FROM dbo.Deals d
    JOIN dbo.Clients c ON d.ClientId = c.Id
    JOIN dbo.TeamMembers tm ON d.OwnerId = tm.Id
    LEFT JOIN dbo.DealDetails dd ON d.Id = dd.DealId
    WHERE d.Id = @DealId;

    -- Набір 2: Позиції угоди (із незмінними знімками цін)
    SELECT 
        di.DealId,
        di.ProductId,
        di.ProductName,
        di.UnitPrice,
        di.Discount,
        di.Quantity,
        di.TotalPrice
    FROM dbo.DealItems di
    WHERE di.DealId = @DealId;

    -- Набір 3: Хронологічний аудит переходів по етапах воронки
    SELECT 
        h.Id,
        h.DealId,
        h.OldStatus,
        h.NewStatus,
        h.ChangedAt,
        h.ChangedBy,
        h.Reason
    FROM dbo.DealStatusHistory h
    WHERE h.DealId = @DealId
    ORDER BY h.ChangedAt ASC;
END;
GO

-- ============================================================================
-- 3. usp_AdvanceDealStage
-- Призначення: Послідовний рух угоди строго на один етап вперед по воронці продажів
--              із блокуванням рядка (UPDLOCK, ROWLOCK) та фіксацією запису в аудит-журналі.
-- Параметри:
--   @DealId BIGINT - Ідентифікатор угоди
--   @ChangedBy NVARCHAR(100) - Користувач, що змінює етап
--   @Reason NVARCHAR(500) - Коментар / причина переходу
-- Допустимі переходи:
--   Lead -> Qualified
--   Qualified -> ProposalSent
--   ProposalSent -> Negotiation
--   (Перехід у Won заборонено, оскільки вимагає виклику usp_WinDeal з бізнес-валідацією).
-- Коди помилок (THROW):
--   50001: Угоду не знайдено або її видалено.
--   50002: Спроба перескочити етапи, завершити угоду без правил або змінити статус закритої угоди.
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.usp_AdvanceDealStage
    @DealId BIGINT,
    @ChangedBy NVARCHAR(100) = N'SalesManager',
    @Reason NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @CurrentStage NVARCHAR(50);
    DECLARE @NextStage NVARCHAR(50);

    SELECT @CurrentStage = Stage
    FROM dbo.Deals WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @DealId AND IsDeleted = 0;

    IF @CurrentStage IS NULL
    BEGIN
        THROW 50001, N'Угоду не знайдено або її видалено.', 1;
    END;

    -- Визначаємо наступний допустимий етап
    IF @CurrentStage = N'Lead'
        SET @NextStage = N'Qualified';
    ELSE IF @CurrentStage = N'Qualified'
        SET @NextStage = N'ProposalSent';
    ELSE IF @CurrentStage = N'ProposalSent'
        SET @NextStage = N'Negotiation';
    ELSE IF @CurrentStage = N'Negotiation'
    BEGIN
        -- Перехід у Won має суворі бізнес-правила (позиції + сума > 0)
        THROW 50002, N'Перехід з етапу Negotiation у Won можливий виключно через процедуру usp_WinDeal.', 1;
    END;
    ELSE
    BEGIN
        THROW 50002, N'Неможливо змінити етап: угода вже знаходиться в кінцевому статусі (Won або Lost).', 1;
    END;

    -- Оновлення етапу угоди
    UPDATE dbo.Deals
    SET Stage = @NextStage,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ChangedBy
    WHERE Id = @DealId;

    -- Запис в історію
    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES (@DealId, @CurrentStage, @NextStage, SYSUTCDATETIME(), @ChangedBy, 
            ISNULL(@Reason, CONCAT(N'Плановий перехід на етап: ', @NextStage)));

    COMMIT TRANSACTION;
END;
GO

-- ============================================================================
-- 4. usp_WinDeal
-- Призначення: Фіналізація успішної угоди (переведення в етап Won) з обов'язковою
--              перевіркою наявності послуг/товарів та додатної суми угоди.
-- Параметри:
--   @DealId BIGINT - Ідентифікатор угоди
--   @ChangedBy NVARCHAR(100) - Користувач, що фіналізує угоду
--   @Reason NVARCHAR(500) - Коментар успішного закриття
-- Допустимі переходи:
--   Лише Negotiation -> Won (перескакувати етапи суворо заборонено).
-- Коди помилок (THROW):
--   50001: Угоду не знайдено або її видалено.
--   50002: Неприпустимий перехід (поточний статус не є Negotiation).
--   50003: В угоді відсутні позиції або загальна сума дорівнює нулю.
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.usp_WinDeal
    @DealId BIGINT,
    @ChangedBy NVARCHAR(100) = N'SalesManager',
    @Reason NVARCHAR(500) = N'Успішне підписання договору з клієнтом'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    DECLARE @CurrentStage NVARCHAR(50);
    DECLARE @TotalAmount DECIMAL(18,2);

    SELECT 
        @CurrentStage = Stage,
        @TotalAmount = TotalAmount
    FROM dbo.Deals WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @DealId AND IsDeleted = 0;

    IF @CurrentStage IS NULL
    BEGIN
        THROW 50001, N'Угоду не знайдено або її видалено.', 1;
    END;

    -- Перевірка 1: Перескакувати етапи заборонено. Won можливий лише з Negotiation!
    IF @CurrentStage <> N'Negotiation'
    BEGIN
        THROW 50002, N'Неприпустимий перехід. Виграти угоду (Won) можна виключно з етапу Negotiation.', 1;
    END;

    -- Перевірка 2: Наявність хоча б однієї позиції та сума > 0
    DECLARE @ItemsCount INT, @ItemsSum DECIMAL(18,2);
    SELECT 
        @ItemsCount = COUNT(*),
        @ItemsSum = ISNULL(SUM(TotalPrice), 0.00)
    FROM dbo.DealItems
    WHERE DealId = @DealId;

    IF @ItemsCount = 0 OR @ItemsSum <= 0.00
    BEGIN
        THROW 50003, N'Неможливо виграти угоду без доданих товарів/послуг або якщо загальна сума дорівнює нулю.', 1;
    END;

    -- Оновлення етапу
    UPDATE dbo.Deals
    SET Stage = N'Won',
        TotalAmount = @ItemsSum,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ChangedBy
    WHERE Id = @DealId;

    -- Запис в історію
    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES (@DealId, @CurrentStage, N'Won', SYSUTCDATETIME(), @ChangedBy, @Reason);

    COMMIT TRANSACTION;
END;
GO

-- ============================================================================
-- 5. usp_LoseDeal
-- Призначення: Закриття неуспішної угоди (переведення в етап Lost) з обов'язковою
--              фіксацією причини зриву угоди в DealDetails.LostReason та аудиті.
-- Параметри:
--   @DealId BIGINT - Ідентифікатор угоди
--   @Reason NVARCHAR(500) - Обов'язкова причина програшу угоди
--   @ChangedBy NVARCHAR(100) - Користувач, що фіксує програш
-- Допустимі переходи:
--   Будь-який відкритий етап (Lead, Qualified, ProposalSent, Negotiation) -> Lost.
--   Заборонено переводити з Won або повторно з Lost.
-- Коди помилок (THROW):
--   50001: Угоду не знайдено або її видалено.
--   50002: Спроба перевести виграну (Won) або вже програну (Lost) угоду.
--   50005: Причина програшу (@Reason) не вказана або порожня.
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.usp_LoseDeal
    @DealId BIGINT,
    @Reason NVARCHAR(500),
    @ChangedBy NVARCHAR(100) = N'SalesManager'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- Перевірка 1: Причина є обов'язковою
    IF @Reason IS NULL OR LTRIM(RTRIM(@Reason)) = N''
    BEGIN
        THROW 50005, N'Причина програшу угоди (@Reason) є обов''язковою для заповнення.', 1;
    END;

    DECLARE @CurrentStage NVARCHAR(50);

    SELECT @CurrentStage = Stage
    FROM dbo.Deals WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @DealId AND IsDeleted = 0;

    IF @CurrentStage IS NULL
    BEGIN
        THROW 50001, N'Угоду не знайдено або її видалено.', 1;
    END;

    -- Перевірка 2: Не можна програти після Won
    IF @CurrentStage = N'Won'
    BEGIN
        THROW 50002, N'Неможливо перевести виграну угоду (Won) у статус Lost.', 1;
    END;

    IF @CurrentStage = N'Lost'
    BEGIN
        THROW 50002, N'Угода вже знаходиться у статусі Lost.', 1;
    END;

    -- Оновлення етапу угоди
    UPDATE dbo.Deals
    SET Stage = N'Lost',
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ChangedBy
    WHERE Id = @DealId;

    -- Фіксація причини програшу в деталях
    UPDATE dbo.DealDetails
    SET LostReason = @Reason,
        UpdatedAt = SYSUTCDATETIME(),
        UpdatedBy = @ChangedBy
    WHERE DealId = @DealId;

    -- Запис в історію
    INSERT INTO dbo.DealStatusHistory (DealId, OldStatus, NewStatus, ChangedAt, ChangedBy, Reason)
    VALUES (@DealId, @CurrentStage, N'Lost', SYSUTCDATETIME(), @ChangedBy, @Reason);

    COMMIT TRANSACTION;
END;
GO

-- ============================================================================
-- 6. usp_AddDealItem
-- Призначення: Додавання або оновлення кількості позиції у відкритій угоді із
--              фіксацією незмінного знімка назви та ціни з локальної таблиці Products,
--              валідацією знижки та перерахунком суми угоди TotalAmount.
-- Параметри:
--   @DealId BIGINT - Ідентифікатор угоди
--   @ProductId BIGINT - ID послуги/товару з локальної копії каталогу
--   @Quantity INT - Кількість (має бути > 0)
--   @Discount DECIMAL(18,2) - Абсолютна знижка (0 <= Discount <= Quantity * UnitPrice)
--   @AddedBy NVARCHAR(100) - Користувач, що додає позицію
-- Допустимі переходи:
--   Дозволено лише для відкритих угод (Lead, Qualified, ProposalSent, Negotiation).
--   Заборонено для закритих угод (Won, Lost).
-- Коди помилок (THROW):
--   50001: Угоду не знайдено або видалено.
--   50002: Спроба додати позицію у закриту угоду (Won чи Lost).
--   50003: Некоректна кількість (<= 0) або знижка перевищує вартість позиції.
--   50004: Товар не знайдено або деактивовано в локальній репліці каталогу.
-- ============================================================================
CREATE OR ALTER PROCEDURE dbo.usp_AddDealItem
    @DealId BIGINT,
    @ProductId BIGINT,
    @Quantity INT = 1,
    @Discount DECIMAL(18,2) = 0.00,
    @AddedBy NVARCHAR(100) = N'SalesManager'
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    -- 1. Перевірка угоди та її статусу
    DECLARE @Stage NVARCHAR(50);

    SELECT @Stage = Stage
    FROM dbo.Deals WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @DealId AND IsDeleted = 0;

    IF @Stage IS NULL
    BEGIN
        THROW 50001, N'Deal not found or deleted', 1;
    END;

    -- Додавати позиції можна лише у відкриті угоди
    IF @Stage IN (N'Won', N'Lost')
    BEGIN
        THROW 50002, N'Cannot add item to closed deal (Won or Lost)', 1;
    END;

    -- 2. Перевірка наявності товару/послуги в локальній репліці
    DECLARE @ProductName NVARCHAR(200);
    DECLARE @UnitPrice DECIMAL(18,2);

    SELECT 
        @ProductName = Name,
        @UnitPrice = Price
    FROM dbo.Products WITH (UPDLOCK, ROWLOCK)
    WHERE Id = @ProductId AND IsActive = 1;

    IF @ProductName IS NULL
    BEGIN
        THROW 50004, N'Product not found or inactive in catalog copy', 1;
    END;

    IF @Quantity <= 0
    BEGIN
        THROW 50003, N'Quantity must be greater than zero', 1;
    END;

    IF @Discount < 0.00 OR @Discount > (@Quantity * @UnitPrice)
    BEGIN
        THROW 50003, N'Discount must be between 0 and total item price', 1;
    END;

    -- 3. Додавання або оновлення позиції (MERGE / UPSERT)
    IF EXISTS (SELECT 1 FROM dbo.DealItems WHERE DealId = @DealId AND ProductId = @ProductId)
    BEGIN
        UPDATE dbo.DealItems
        SET Quantity = Quantity + @Quantity,
            Discount = @Discount
        WHERE DealId = @DealId AND ProductId = @ProductId;
    END
    ELSE
    BEGIN
        INSERT INTO dbo.DealItems (DealId, ProductId, ProductName, UnitPrice, Discount, Quantity, CreatedAt, CreatedBy)
        VALUES (@DealId, @ProductId, @ProductName, @UnitPrice, @Discount, @Quantity, SYSUTCDATETIME(), @AddedBy);
    END;

    -- 4. Перерахунок загальної суми угоди
    UPDATE d
    SET d.TotalAmount = ISNULL((SELECT SUM(TotalPrice) FROM dbo.DealItems WHERE DealId = @DealId), 0.00),
        d.UpdatedAt = SYSUTCDATETIME(),
        d.UpdatedBy = @AddedBy
    FROM dbo.Deals d
    WHERE d.Id = @DealId;

    COMMIT TRANSACTION;
END;
GO
