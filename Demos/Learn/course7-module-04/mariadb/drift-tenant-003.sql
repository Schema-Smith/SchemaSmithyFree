-- Simulate one drifted tenant. fleet_tenant_003's Product.Sku is relaxed to
-- allow NULLs and given a row with a NULL Sku. When the fleet run reconciles
-- the column back to NOT NULL, it fails because the tenant holds a NULL -- one
-- work unit fails while the rest of the fleet succeeds.
-- Run with the fleet_tenant_003 schema selected: mysql ... fleet_tenant_003
DELETE FROM Product;
ALTER TABLE Product MODIFY Sku VARCHAR(64) NULL;
INSERT INTO Product (ProductId, Name, Sku, UnitPrice) VALUES (901, 'Drift', NULL, 9.99);
