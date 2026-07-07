-- Course 8 Module 5 setup: the recovery-toolkit sandbox database (SQL Server).
-- diag_* prefix so Course 8 never collides with Course 6's shop_tenant_* or
-- Course 7's fleet_tenant_* (or M0/M2's diag_* databases) in the shared sandbox.
IF DB_ID('diag_recovery') IS NULL CREATE DATABASE [diag_recovery];
