-- Shared e-commerce shop schema (SQL Server). The post-migration state every
-- Course 5 source tool produces. Idempotent: drops in FK order, then recreates.
IF OBJECT_ID('dbo.OrderItem')  IS NOT NULL DROP TABLE dbo.OrderItem;
IF OBJECT_ID('dbo.SalesOrder') IS NOT NULL DROP TABLE dbo.SalesOrder;
IF OBJECT_ID('dbo.Product')    IS NOT NULL DROP TABLE dbo.Product;
IF OBJECT_ID('dbo.Customer')   IS NOT NULL DROP TABLE dbo.Customer;
CREATE TABLE dbo.Customer (
  CustomerId INT NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
  Email      NVARCHAR(256) NOT NULL,
  FullName   NVARCHAR(200) NULL
);
CREATE TABLE dbo.Product (
  ProductId INT NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
  Sku       VARCHAR(64)   NOT NULL,
  Name      NVARCHAR(200) NOT NULL,
  UnitPrice DECIMAL(10,2) NOT NULL
);
CREATE TABLE dbo.SalesOrder (
  OrderId    INT NOT NULL CONSTRAINT PK_SalesOrder PRIMARY KEY,
  CustomerId INT NOT NULL CONSTRAINT FK_SalesOrder_Customer REFERENCES dbo.Customer(CustomerId),
  OrderDate  DATETIME2   NOT NULL,
  Status     VARCHAR(20) NOT NULL
);
CREATE TABLE dbo.OrderItem (
  OrderItemId INT NOT NULL CONSTRAINT PK_OrderItem PRIMARY KEY,
  OrderId     INT NOT NULL CONSTRAINT FK_OrderItem_SalesOrder REFERENCES dbo.SalesOrder(OrderId),
  ProductId   INT NOT NULL CONSTRAINT FK_OrderItem_Product   REFERENCES dbo.Product(ProductId),
  Quantity    INT NOT NULL,
  UnitPrice   DECIMAL(10,2) NOT NULL
);
GO
