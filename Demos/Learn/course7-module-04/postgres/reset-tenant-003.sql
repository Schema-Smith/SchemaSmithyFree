-- Fix the drift: remove the row holding the NULL sku. The resume then
-- reconciles product back to the package shape (sku NOT NULL) and succeeds.
-- Run against the fleet_tenant_003 database: psql ... -d fleet_tenant_003
DELETE FROM public.product;
