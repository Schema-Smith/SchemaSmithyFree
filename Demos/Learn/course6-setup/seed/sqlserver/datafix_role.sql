-- Course 6 setup — scoped datafix_user login + per-tenant grants (SQL Server).
-- Creates a server-level login and a minimal-privilege user in each of the three
-- tenant databases. Run once against master; the USE statements switch context.
-- These grants are provisional: Task 4 certification may tighten the set.

-- Create the server login (idempotent guard)
IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'datafix_user')
    CREATE LOGIN datafix_user WITH PASSWORD = 'DataFix!Demo123';
GO

-- ── shop_tenant_a ────────────────────────────────────────────────────────────
USE shop_tenant_a;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'datafix_user')
    CREATE USER datafix_user FOR LOGIN datafix_user;
GO

-- Reader/writer on the Shop data (dbo schema, default for this database)
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO datafix_user;

-- Backup table creation: SQL Server requires BOTH of these grants together.
-- CREATE TABLE grants the ability to issue the DDL statement, but the new table
-- must land in a schema — GRANT ALTER ON SCHEMA::dbo lets the user place objects
-- in dbo. Without the ALTER grant, CREATE TABLE succeeds in principle but the
-- engine rejects placement: "The specified schema name dbo either does not exist
-- or you do not have permission to use it."
GRANT CREATE TABLE                TO datafix_user;
GRANT ALTER     ON SCHEMA::dbo   TO datafix_user;

-- Ancillary stored procedures or functions the fix script may call
GRANT EXECUTE  ON SCHEMA::dbo   TO datafix_user;

-- Note: tempdb access for #temp tables is implicit for any authenticated login;
-- no explicit grant is needed.
GO

-- ── shop_tenant_b ────────────────────────────────────────────────────────────
USE shop_tenant_b;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'datafix_user')
    CREATE USER datafix_user FOR LOGIN datafix_user;
GO

GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO datafix_user;
GRANT CREATE TABLE                TO datafix_user;
GRANT ALTER     ON SCHEMA::dbo   TO datafix_user;
GRANT EXECUTE  ON SCHEMA::dbo   TO datafix_user;
GO

-- ── shop_tenant_c ────────────────────────────────────────────────────────────
USE shop_tenant_c;
GO

IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'datafix_user')
    CREATE USER datafix_user FOR LOGIN datafix_user;
GO

GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO datafix_user;
GRANT CREATE TABLE                TO datafix_user;
GRANT ALTER     ON SCHEMA::dbo   TO datafix_user;
GRANT EXECUTE  ON SCHEMA::dbo   TO datafix_user;
GO
