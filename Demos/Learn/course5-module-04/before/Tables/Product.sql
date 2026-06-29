CREATE TABLE [dbo].[Product]
(
    [ProductId] INT            NOT NULL,
    [Sku]       VARCHAR(64)    NOT NULL,
    [Name]      NVARCHAR(200)  NOT NULL,
    [UnitPrice] DECIMAL(10, 2) NOT NULL,
    CONSTRAINT [PK_Product] PRIMARY KEY ([ProductId])
);
