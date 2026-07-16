-- fleet_tenant_004 drift (out-of-band): someone dropped FK_OrderItem_Product and left an
-- orphan OrderItem (ProductId 999 has no parent Product). The rollout doesn't touch this
-- FK -- but the convergence engine re-checks the whole model, finds the FK "missing,"
-- recreates it, and the orphan fails it (MySQL 1452) at the foreign-keys phase.
-- SalesOrder/Customer parents are valid, so ONLY the Product FK fails -- deterministic.
USE fleet_tenant_004;
DELETE FROM OrderItem; DELETE FROM SalesOrder; DELETE FROM Customer;
INSERT INTO Customer (CustomerId, Email, FullName) VALUES (1, 'c1@shop.example', 'Carl Index');
INSERT INTO SalesOrder (OrderId, CustomerId, OrderDate, Status) VALUES (1, 1, NOW(), 'OPEN');
-- Guard: only drop FK if it still exists (makes the script safe to re-run).
SET @fk_exists = (
  SELECT COUNT(*) FROM information_schema.TABLE_CONSTRAINTS
  WHERE CONSTRAINT_SCHEMA = 'fleet_tenant_004'
    AND TABLE_NAME = 'OrderItem'
    AND CONSTRAINT_NAME = 'FK_OrderItem_Product'
    AND CONSTRAINT_TYPE = 'FOREIGN KEY'
);
SET @drop_fk = IF(@fk_exists > 0, 'ALTER TABLE OrderItem DROP FOREIGN KEY FK_OrderItem_Product', 'SELECT 1');
PREPARE stmt FROM @drop_fk;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;
INSERT INTO OrderItem (OrderItemId, OrderId, ProductId, Quantity, UnitPrice) VALUES (1, 1, 999, 1, 1.00);
