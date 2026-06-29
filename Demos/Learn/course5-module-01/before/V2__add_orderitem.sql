-- Flyway migration V2 — add order lines.
CREATE TABLE OrderItem (
    OrderItemId INT NOT NULL CONSTRAINT PK_OrderItem PRIMARY KEY,
    OrderId     INT NOT NULL CONSTRAINT FK_OrderItem_SalesOrder REFERENCES SalesOrder(OrderId),
    ProductId   INT NOT NULL CONSTRAINT FK_OrderItem_Product   REFERENCES Product(ProductId),
    Quantity    INT NOT NULL,
    UnitPrice   DECIMAL(10,2) NOT NULL
);
