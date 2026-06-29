-- Flyway migration V1 — create the shop core.
-- This is the SQL Server variant of a Flyway project (Flyway migrations are
-- written per database engine). Shown for reference only: you do NOT run Flyway
-- in this lab. The setup already applied the end state to shop_from_flyway.
CREATE TABLE Customer (
    CustomerId INT NOT NULL CONSTRAINT PK_Customer PRIMARY KEY,
    Email      NVARCHAR(256) NOT NULL,
    FullName   NVARCHAR(200) NULL
);

CREATE TABLE Product (
    ProductId INT NOT NULL CONSTRAINT PK_Product PRIMARY KEY,
    Sku       VARCHAR(64)   NOT NULL,
    Name      NVARCHAR(200) NOT NULL,
    UnitPrice DECIMAL(10,2) NOT NULL
);

CREATE TABLE SalesOrder (
    OrderId    INT NOT NULL CONSTRAINT PK_SalesOrder PRIMARY KEY,
    CustomerId INT NOT NULL CONSTRAINT FK_SalesOrder_Customer REFERENCES Customer(CustomerId),
    OrderDate  DATETIME2   NOT NULL,
    Status     VARCHAR(20) NOT NULL
);
