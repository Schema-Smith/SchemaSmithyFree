-- Course 8 Module 1 setup: the "reading the black box" sandbox database (SQL Server).
-- diag_* prefix so Course 8 never collides with Course 6's shop_tenant_* or
-- Course 7's fleet_tenant_* (or M0's diag_baseline) in the shared sandbox.
IF DB_ID('diag_blackbox') IS NULL CREATE DATABASE [diag_blackbox];
