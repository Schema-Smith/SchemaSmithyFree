CREATE TABLE [dbo].[SalesOrder]
(
    [OrderId]    INT          NOT NULL,
    [CustomerId] INT          NOT NULL,
    [OrderDate]  DATETIME2    NOT NULL,
    [Status]     VARCHAR(20)  NOT NULL,
    CONSTRAINT [PK_SalesOrder] PRIMARY KEY ([OrderId]),
    CONSTRAINT [FK_SalesOrder_Customer] FOREIGN KEY ([CustomerId]) REFERENCES [dbo].[Customer] ([CustomerId])
);
