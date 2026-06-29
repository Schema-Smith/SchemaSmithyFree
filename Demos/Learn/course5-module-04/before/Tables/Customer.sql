CREATE TABLE [dbo].[Customer]
(
    [CustomerId] INT            NOT NULL,
    [Email]      NVARCHAR(256)  NOT NULL,
    [FullName]   NVARCHAR(200)  NULL,
    CONSTRAINT [PK_Customer] PRIMARY KEY ([CustomerId])
);
