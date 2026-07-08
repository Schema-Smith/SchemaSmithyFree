-- Fix the drift: collapse the duplicate email so the unique index builds on resume.
-- Run against the fleet_tenant_002 database: psql ... -d fleet_tenant_002
UPDATE public.customer SET email = 'grace@shop.example' WHERE customerid = 2;
