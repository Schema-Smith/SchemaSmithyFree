-- Course 8 Module 4 setup: the script-slot + data-delivery sandbox database (SQL Server).
-- diag_* prefix so Course 8 never collides with Course 6's shop_tenant_* or
-- Course 7's fleet_tenant_* (or M0/M2's diag_* databases) in the shared sandbox.
IF DB_ID('diag_scripts') IS NULL CREATE DATABASE [diag_scripts];
