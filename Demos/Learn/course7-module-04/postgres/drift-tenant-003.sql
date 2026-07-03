-- Simulate one drifted tenant. fleet_tenant_003's product.sku is relaxed to
-- allow NULLs and given a row with a NULL sku. When the fleet run reconciles
-- the column back to NOT NULL, it fails because the tenant holds a NULL -- one
-- work unit fails while the rest of the fleet succeeds.
-- Run against the fleet_tenant_003 database: psql ... -d fleet_tenant_003
DELETE FROM public.product;
ALTER TABLE public.product ALTER COLUMN sku DROP NOT NULL;
INSERT INTO public.product (productid, name, sku, unitprice) VALUES (901, 'Drift', NULL, 9.99);
