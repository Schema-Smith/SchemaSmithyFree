-- Course 6 setup — Shop schema + price-defect seed (MySQL; schema = database). Idempotent.
-- Three tenant databases are expected to already exist when this runs;
-- the setup script creates them. Re-running drops and recreates everything.

SET FOREIGN_KEY_CHECKS = 0;
DROP TABLE IF EXISTS `OrderItem`, `SalesOrder`, `Product`, `Customer`;
SET FOREIGN_KEY_CHECKS = 1;

CREATE TABLE `Customer` (
  `CustomerId` INT           NOT NULL,
  `Email`      VARCHAR(256)  NOT NULL,
  `FullName`   VARCHAR(200)  NULL,
  CONSTRAINT `PK_Customer` PRIMARY KEY (`CustomerId`)
) ENGINE=InnoDB;

CREATE TABLE `Product` (
  `ProductId` INT           NOT NULL,
  `Sku`       VARCHAR(64)   NOT NULL,
  `Name`      VARCHAR(200)  NOT NULL,
  `UnitPrice` DECIMAL(10,2) NOT NULL,
  CONSTRAINT `PK_Product` PRIMARY KEY (`ProductId`)
) ENGINE=InnoDB;

CREATE TABLE `SalesOrder` (
  `OrderId`    INT          NOT NULL,
  `CustomerId` INT          NOT NULL,
  `OrderDate`  DATETIME     NOT NULL,
  `Status`     VARCHAR(20)  NOT NULL,
  CONSTRAINT `PK_SalesOrder`         PRIMARY KEY (`OrderId`),
  CONSTRAINT `FK_SalesOrder_Customer` FOREIGN KEY (`CustomerId`) REFERENCES `Customer`(`CustomerId`)
) ENGINE=InnoDB;

CREATE TABLE `OrderItem` (
  `OrderItemId` INT           NOT NULL,
  `OrderId`     INT           NOT NULL,
  `ProductId`   INT           NOT NULL,
  `Quantity`    INT           NOT NULL,
  `UnitPrice`   DECIMAL(10,2) NOT NULL,
  CONSTRAINT `PK_OrderItem`            PRIMARY KEY (`OrderItemId`),
  CONSTRAINT `FK_OrderItem_SalesOrder` FOREIGN KEY (`OrderId`)    REFERENCES `SalesOrder`(`OrderId`),
  CONSTRAINT `FK_OrderItem_Product`    FOREIGN KEY (`ProductId`)  REFERENCES `Product`(`ProductId`)
) ENGINE=InnoDB;

-- Reference data
INSERT INTO `Customer` (`CustomerId`, `Email`, `FullName`) VALUES
  (1, 'alice@example.com',   'Alice Nguyen'),
  (2, 'bob@example.com',     'Bob Marsh'),
  (3, 'carol@example.com',   'Carol Simmons'),
  (4, 'david@example.com',   'David Park'),
  (5, 'eve@example.com',     'Eve Torres');

INSERT INTO `Product` (`ProductId`, `Sku`, `Name`, `UnitPrice`) VALUES
  (1, 'WDG-001', 'Widget Alpha',    49.99),
  (2, 'WDG-002', 'Widget Beta',     79.99),
  (3, 'GAD-001', 'Gadget Pro',     129.99),
  (4, 'GAD-002', 'Gadget Lite',     59.99),
  (5, 'ACC-001', 'Accessory Pack',  19.99);

-- SalesOrders: April, May, and June 2026.
-- May orders get the buggy double-discount on their OrderItems.
INSERT INTO `SalesOrder` (`OrderId`, `CustomerId`, `OrderDate`, `Status`) VALUES
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
INSERT INTO `OrderItem` (`OrderItemId`, `OrderId`, `ProductId`, `Quantity`, `UnitPrice`) VALUES
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
