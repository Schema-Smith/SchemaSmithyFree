-- fleet_tenant_004 drift (out-of-band): someone dropped FK_OrderItem_Product and left an
-- orphan OrderItem (ProductId 999 has no parent Product). The rollout doesn't touch this
-- FK -- but the convergence engine re-checks the whole model, finds the FK "missing,"
-- recreates it WITH CHECK, and the orphan fails it (547) at the foreign-keys phase.
-- SalesOrder/Customer parents are valid, so ONLY the Product FK fails -- deterministic.
USE [fleet_tenant_004];
DELETE FROM dbo.OrderItem; DELETE FROM dbo.SalesOrder; DELETE FROM dbo.Customer;
INSERT INTO dbo.Customer (CustomerId, Email, FullName) VALUES (1, 'c1@shop.example', 'Carl Index');
INSERT INTO dbo.SalesOrder (OrderId, CustomerId, OrderDate, Status) VALUES (1, 1, SYSUTCDATETIME(), 'OPEN');
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_OrderItem_Product')
  ALTER TABLE dbo.OrderItem DROP CONSTRAINT FK_OrderItem_Product;
INSERT INTO dbo.OrderItem (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) VALUES (1, 1, 999, 1, 1.00);
