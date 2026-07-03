-- Simulate one drifted tenant. fleet_tenant_003's Product.Sku is relaxed to
-- allow NULLs and given a row with a NULL Sku. When the fleet run reconciles
-- the column back to NOT NULL, it fails because the tenant holds a NULL -- one
-- work unit fails while the rest of the fleet succeeds.
USE [fleet_tenant_003];
DELETE FROM dbo.Product;
ALTER TABLE dbo.Product ALTER COLUMN Sku VARCHAR(64) NULL;
INSERT INTO dbo.Product (ProductId, Name, Sku, UnitPrice) VALUES (901, 'Drift', NULL, 9.99);
