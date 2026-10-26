IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
CREATE TABLE [dbo].[Category] (
    [Id] bigint NOT NULL IDENTITY,
    [Name] nvarchar(100) NOT NULL,
    [Description] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(100) NOT NULL DEFAULT N'system',
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(100) NULL,
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    CONSTRAINT [PK_Category] PRIMARY KEY ([Id])
);

CREATE TABLE [dbo].[Product] (
    [Id] bigint NOT NULL IDENTITY,
    [Sku] nvarchar(50) NOT NULL,
    [Name] nvarchar(200) NOT NULL,
    [ItemType] nvarchar(20) NOT NULL DEFAULT N'Service',
    [Price] decimal(18,2) NOT NULL,
    [StockQuantity] int NOT NULL DEFAULT 0,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [IsDeleted] bit NOT NULL DEFAULT CAST(0 AS bit),
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(100) NOT NULL DEFAULT N'system',
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(100) NULL,
    [RowVersion] rowversion NOT NULL,
    CONSTRAINT [PK_Product] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_Product_ItemType] CHECK ([ItemType] IN (N'Service', N'Product')),
    CONSTRAINT [CK_Product_Price] CHECK ([Price] > 0)
);

CREATE TABLE [dbo].[PriceHistory] (
    [Id] bigint NOT NULL IDENTITY,
    [ProductId] bigint NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [ValidFrom] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [ValidTo] datetime2 NULL,
    [ChangedBy] nvarchar(100) NOT NULL DEFAULT N'system',
    [Reason] nvarchar(250) NULL,
    CONSTRAINT [PK_PriceHistory] PRIMARY KEY ([Id]),
    CONSTRAINT [CK_PriceHistory_Price] CHECK ([Price] > 0),
    CONSTRAINT [CK_PriceHistory_ValidTo] CHECK ([ValidTo] IS NULL OR [ValidTo] > [ValidFrom]),
    CONSTRAINT [FK_PriceHistory_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[ProductCategory] (
    [ProductId] bigint NOT NULL,
    [CategoryId] bigint NOT NULL,
    [AddedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT [PK_ProductCategory] PRIMARY KEY ([ProductId], [CategoryId]),
    CONSTRAINT [FK_ProductCategory_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [dbo].[Category] ([Id]) ON DELETE CASCADE,
    CONSTRAINT [FK_ProductCategory_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[ProductDetail] (
    [ProductId] bigint NOT NULL,
    [Description] nvarchar(max) NOT NULL,
    [Unit] nvarchar(50) NOT NULL DEFAULT N'послуга',
    [DurationMinutes] int NULL,
    [TaxRate] decimal(5,2) NOT NULL DEFAULT 20.0,
    [CreatedAt] datetime2 NOT NULL DEFAULT (SYSUTCDATETIME()),
    [CreatedBy] nvarchar(100) NOT NULL DEFAULT N'system',
    [UpdatedAt] datetime2 NULL,
    [UpdatedBy] nvarchar(100) NULL,
    CONSTRAINT [PK_ProductDetail] PRIMARY KEY ([ProductId]),
    CONSTRAINT [FK_ProductDetail_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]) ON DELETE CASCADE
);

CREATE TABLE [dbo].[ProductImage] (
    [Id] bigint NOT NULL IDENTITY,
    [ProductId] bigint NOT NULL,
    [Url] nvarchar(500) NOT NULL,
    [AltText] nvarchar(200) NULL,
    [IsMain] bit NOT NULL DEFAULT CAST(0 AS bit),
    [SortOrder] int NOT NULL DEFAULT 0,
    CONSTRAINT [PK_ProductImage] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductImage_Product_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([Id]) ON DELETE CASCADE
);

IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Description', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[dbo].[Category]'))
    SET IDENTITY_INSERT [dbo].[Category] ON;
INSERT INTO [dbo].[Category] ([Id], [CreatedAt], [CreatedBy], [Description], [Name], [UpdatedAt], [UpdatedBy])
VALUES (CAST(1 AS bigint), '2026-01-01T00:00:00.0000000Z', N'seed', N'Створення односторінкових сайтів, порталів та веб-сервісів', N'Веб-розробка', NULL, NULL),
(CAST(2 AS bigint), '2026-01-01T00:00:00.0000000Z', N'seed', N'Супровід, оновлення безпеки та моніторинг стабільності', N'Технічна підтримка', NULL, NULL),
(CAST(3 AS bigint), '2026-01-01T00:00:00.0000000Z', N'seed', N'Технічний аудит, оптимізація семантики та просування', N'Пошукова оптимізація (SEO)', NULL, NULL),
(CAST(4 AS bigint), '2026-01-01T00:00:00.0000000Z', N'seed', N'Таргетована реклама, контекст та просування в соцмережах', N'Маркетинг та реклама (SMM/PPC)', NULL, NULL),
(CAST(5 AS bigint), '2026-01-01T00:00:00.0000000Z', N'seed', N'Розробка логотипів, брендбуків та користувацьких інтерфейсів UI/UX', N'Дизайн та айдентика', NULL, NULL),
(CAST(6 AS bigint), '2026-01-01T00:00:00.0000000Z', N'seed', N'Хостинг, SSL-сертифікати та ліцензійне ПЗ', N'Хмарна інфраструктура та ліцензії', NULL, NULL);
IF EXISTS (SELECT * FROM [sys].[identity_columns] WHERE [name] IN (N'Id', N'CreatedAt', N'CreatedBy', N'Description', N'Name', N'UpdatedAt', N'UpdatedBy') AND [object_id] = OBJECT_ID(N'[dbo].[Category]'))
    SET IDENTITY_INSERT [dbo].[Category] OFF;

CREATE UNIQUE INDEX [IX_Category_Name] ON [dbo].[Category] ([Name]);

CREATE INDEX [IX_PriceHistory_ProductId_ValidFrom] ON [dbo].[PriceHistory] ([ProductId], [ValidFrom]);

CREATE INDEX [IX_Product_Name] ON [dbo].[Product] ([Name]);

CREATE INDEX [IX_Product_Price] ON [dbo].[Product] ([Price]);

CREATE UNIQUE INDEX [IX_Product_Sku] ON [dbo].[Product] ([Sku]);

CREATE INDEX [IX_ProductCategory_CategoryId] ON [dbo].[ProductCategory] ([CategoryId]);

CREATE INDEX [IX_ProductImage_ProductId] ON [dbo].[ProductImage] ([ProductId]);

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20261026160556_InitialCreate', N'10.0.14');

COMMIT;
GO

