-- Fix the drift: remove the row holding the NULL Sku. The resume then
-- reconciles Product back to the package shape (Sku NOT NULL) and succeeds.
-- Run with the fleet_tenant_003 schema selected: mysql ... fleet_tenant_003
DELETE FROM Product;
