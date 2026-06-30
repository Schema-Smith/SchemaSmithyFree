-- Course 6 setup — Shop schema + price-defect seed (SQL Server). Idempotent.
-- Three tenant databases are expected to already exist when this runs;
-- the setup script creates them. Re-running drops and recreates everything.

-- Drop in FK order
IF OBJECT_ID('dbo.OrderItem')  IS NOT NULL DROP TABLE dbo.OrderItem;
IF OBJECT_ID('dbo.SalesOrder') IS NOT NULL DROP TABLE dbo.SalesOrder;
IF OBJECT_ID('dbo.Product')    IS NOT NULL DROP TABLE dbo.Product;
IF OBJECT_ID('dbo.Customer')   IS NOT NULL DROP TABLE dbo.Customer;

CREATE TABLE dbo.Customer (
  CustomerId INT           NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
  Email      NVARCHAR(256) NOT NULL,
  FullName   NVARCHAR(200) NULL
);
CREATE TABLE dbo.Product (
  ProductId INT           NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
  Sku       VARCHAR(64)   NOT NULL,
  Name      NVARCHAR(200) NOT NULL,
  UnitPrice DECIMAL(10,2) NOT NULL
);
CREATE TABLE dbo.SalesOrder (
  OrderId    INT          NOT NULL CONSTRAINT PK_SalesOrder PRIMARY KEY,
  CustomerId INT          NOT NULL CONSTRAINT FK_SalesOrder_Customer REFERENCES dbo.Customer(CustomerId),
  OrderDate  DATETIME2    NOT NULL,
  Status     VARCHAR(20)  NOT NULL
);
CREATE TABLE dbo.OrderItem (
  OrderItemId INT           NOT NULL CONSTRAINT PK_OrderItem PRIMARY KEY,
  OrderId     INT           NOT NULL CONSTRAINT FK_OrderItem_SalesOrder REFERENCES dbo.SalesOrder(OrderId),
  ProductId   INT           NOT NULL CONSTRAINT FK_OrderItem_Product    REFERENCES dbo.Product(ProductId),
  Quantity    INT           NOT NULL,
  UnitPrice   DECIMAL(10,2) NOT NULL
);
GO

-- Reference data
INSERT INTO dbo.Customer (CustomerId, Email, FullName) VALUES
  (1, 'alice@example.com',   'Alice Nguyen'),
  (2, 'bob@example.com',     'Bob Marsh'),
  (3, 'carol@example.com',   'Carol Simmons'),
  (4, 'david@example.com',   'David Park'),
  (5, 'eve@example.com',     'Eve Torres');

INSERT INTO dbo.Product (ProductId, Sku, Name, UnitPrice) VALUES
  (1, 'WDG-001', 'Widget Alpha',   49.99),
  (2, 'WDG-002', 'Widget Beta',    79.99),
  (3, 'GAD-001', 'Gadget Pro',    129.99),
  (4, 'GAD-002', 'Gadget Lite',    59.99),
  (5, 'ACC-001', 'Accessory Pack', 19.99);

-- SalesOrders: April, May, and June 2026.
-- May orders get the buggy double-discount on their OrderItems.
INSERT INTO dbo.SalesOrder (OrderId, CustomerId, OrderDate, Status) VALUES
  -- April 2026
  (101, 1, '2026-04-03', 'Completed'),
  (102, 2, '2026-04-10', 'Completed'),
  (103, 3, '2026-04-18', 'Completed'),
  (104, 4, '2026-04-25', 'Completed'),
  -- May 2026  (buggy batch)
  (105, 1, '2026-05-02', 'Completed'),
  (106, 2, '2026-05-09', 'Completed'),
  (107, 3, '2026-05-14', 'Completed'),
  (108, 4, '2026-05-21', 'Completed'),
  (109, 5, '2026-05-28', 'Completed'),
  -- June 2026
  (110, 5, '2026-06-04', 'Completed'),
  (111, 1, '2026-06-11', 'Completed'),
  (112, 2, '2026-06-18', 'Completed');

-- OrderItems.
-- April orders: intended single 10% discount => UnitPrice = ROUND(p.UnitPrice * 0.90, 2)
-- May orders:   bug — 10% applied twice => UnitPrice = ROUND(p.UnitPrice * 0.81, 2)
-- June orders:  intended single 10% discount => UnitPrice = ROUND(p.UnitPrice * 0.90, 2)
INSERT INTO dbo.OrderItem (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) VALUES
  -- April orders (correct: *0.90)
  (1001, 101, 1, 2, ROUND(49.99 * 0.90, 2)),
  (1002, 101, 5, 1, ROUND(19.99 * 0.90, 2)),
  (1003, 102, 2, 1, ROUND(79.99 * 0.90, 2)),
  (1004, 102, 3, 1, ROUND(129.99 * 0.90, 2)),
  (1005, 103, 4, 3, ROUND(59.99 * 0.90, 2)),
  (1006, 104, 1, 1, ROUND(49.99 * 0.90, 2)),
  (1007, 104, 2, 2, ROUND(79.99 * 0.90, 2)),
  -- May orders (buggy: *0.81)
  (1008, 105, 1, 1, ROUND(49.99 * 0.81, 2)),
  (1009, 105, 3, 1, ROUND(129.99 * 0.81, 2)),
  (1010, 106, 2, 2, ROUND(79.99 * 0.81, 2)),
  (1011, 106, 4, 1, ROUND(59.99 * 0.81, 2)),
  (1012, 107, 5, 4, ROUND(19.99 * 0.81, 2)),
  (1013, 107, 1, 2, ROUND(49.99 * 0.81, 2)),
  (1014, 108, 3, 1, ROUND(129.99 * 0.81, 2)),
  (1015, 108, 2, 1, ROUND(79.99 * 0.81, 2)),
  (1016, 109, 4, 2, ROUND(59.99 * 0.81, 2)),
  (1017, 109, 5, 3, ROUND(19.99 * 0.81, 2)),
  -- June orders (correct: *0.90)
  (1018, 110, 3, 1, ROUND(129.99 * 0.90, 2)),
  (1019, 110, 4, 2, ROUND(59.99 * 0.90, 2)),
  (1020, 111, 1, 1, ROUND(49.99 * 0.90, 2)),
  (1021, 111, 5, 2, ROUND(19.99 * 0.90, 2)),
  (1022, 112, 2, 1, ROUND(79.99 * 0.90, 2)),
  (1023, 112, 3, 1, ROUND(129.99 * 0.90, 2));
GO
