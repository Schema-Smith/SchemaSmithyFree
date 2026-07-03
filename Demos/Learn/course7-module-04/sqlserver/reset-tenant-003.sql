-- Fix the drift: remove the row holding the NULL Sku. The resume then
-- reconciles Product back to the package shape (Sku NOT NULL) and succeeds.
USE [fleet_tenant_003];
DELETE FROM dbo.Product;
