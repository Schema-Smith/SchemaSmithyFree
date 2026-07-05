-- Course 8 setup: the diagnostics baseline database (SQL Server).
-- diag_* prefix so Course 8 never collides with Course 6's shop_tenant_* or
-- Course 7's fleet_tenant_* in the shared sandbox.
IF DB_ID('diag_baseline') IS NULL CREATE DATABASE [diag_baseline];
