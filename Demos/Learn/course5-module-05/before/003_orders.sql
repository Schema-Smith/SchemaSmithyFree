-- 003: two tables in one file (it was easier at the time). SalesOrder has no
-- Status column yet — that came later, in 004. Note SalesOrder is guarded but
-- OrderItem isn't. More drift.
IF OBJECT_ID('dbo.SalesOrder') IS NULL
BEGIN
  CREATE TABLE dbo.SalesOrder (
    OrderId    INT NOT NULL CONSTRAINT PK_SalesOrder PRIMARY KEY,
    CustomerId INT NOT NULL CONSTRAINT FK_SalesOrder_Customer REFERENCES dbo.Customer(CustomerId),
    OrderDate  DATETIME2 NOT NULL
  );
END;

CREATE TABLE dbo.OrderItem (
  OrderItemId INT NOT NULL CONSTRAINT PK_OrderItem PRIMARY KEY,
  OrderId     INT NOT NULL CONSTRAINT FK_OrderItem_SalesOrder REFERENCES dbo.SalesOrder(OrderId),
  ProductId   INT NOT NULL CONSTRAINT FK_OrderItem_Product   REFERENCES dbo.Product(ProductId),
  Quantity    INT NOT NULL,
  UnitPrice   DECIMAL(10,2) NOT NULL
);

INSERT INTO dbo.schema_version (version, description) VALUES (3, '003_orders.sql');
