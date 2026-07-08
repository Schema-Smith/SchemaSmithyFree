-- Fix the drift: remove the orphan orderitem so the recreated FK passes on resume.
-- Run against the fleet_tenant_004 database: psql ... -d fleet_tenant_004
DELETE FROM public.orderitem WHERE orderitemid = 1;
