CREATE TABLE [dbo].[OrderItem]
(
    [OrderItemId] INT            NOT NULL,
    [OrderId]     INT            NOT NULL,
    [ProductId]   INT            NOT NULL,
    [Quantity]    INT            NOT NULL,
    [UnitPrice]   DECIMAL(10, 2) NOT NULL,
    CONSTRAINT [PK_OrderItem] PRIMARY KEY ([OrderItemId]),
    CONSTRAINT [FK_OrderItem_SalesOrder] FOREIGN KEY ([OrderId]) REFERENCES [dbo].[SalesOrder] ([OrderId]),
    CONSTRAINT [FK_OrderItem_Product] FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Product] ([ProductId])
);
