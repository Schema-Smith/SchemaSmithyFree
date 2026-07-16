-- Shared e-commerce shop schema (MySQL; schema = database). Idempotent.
SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS OrderItem, SalesOrder, Product, Customer;
SET FOREIGN_KEY_CHECKS = 1;
CREATE TABLE Customer (
  CustomerId INT NOT NULL,
  Email      VARCHAR(256) NOT NULL,
  FullName   VARCHAR(200) NULL,
  CONSTRAINT PK_Customer PRIMARY KEY (CustomerId)
) ENGINE=InnoDB;
CREATE TABLE Product (
  ProductId INT NOT NULL,
  Sku       VARCHAR(64)  NOT NULL,
  Name      VARCHAR(200) NOT NULL,
  UnitPrice DECIMAL(10,2) NOT NULL,
  CONSTRAINT PK_Product PRIMARY KEY (ProductId)
) ENGINE=InnoDB;
CREATE TABLE SalesOrder (
  OrderId    INT NOT NULL,
  CustomerId INT NOT NULL,
  OrderDate  DATETIME    NOT NULL,
  Status     VARCHAR(20) NOT NULL,
  CONSTRAINT PK_SalesOrder PRIMARY KEY (OrderId),
  CONSTRAINT FK_SalesOrder_Customer FOREIGN KEY (CustomerId) REFERENCES Customer(CustomerId)
) ENGINE=InnoDB;
CREATE TABLE OrderItem (
  OrderItemId INT NOT NULL,
  OrderId     INT NOT NULL,
  ProductId   INT NOT NULL,
  Quantity    INT NOT NULL,
  UnitPrice   DECIMAL(10,2) NOT NULL,
  CONSTRAINT PK_OrderItem PRIMARY KEY (OrderItemId),
  CONSTRAINT FK_OrderItem_SalesOrder FOREIGN KEY (OrderId)   REFERENCES SalesOrder(OrderId),
  CONSTRAINT FK_OrderItem_Product    FOREIGN KEY (ProductId) REFERENCES Product(ProductId)
) ENGINE=InnoDB;
