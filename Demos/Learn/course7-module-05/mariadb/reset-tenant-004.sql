-- Fix the drift: remove the orphan OrderItem so the recreated FK passes on resume.
-- (The convergence engine recreates FK_OrderItem_Product itself.)
USE fleet_tenant_004;
DELETE FROM OrderItem WHERE OrderItemId = 1;
