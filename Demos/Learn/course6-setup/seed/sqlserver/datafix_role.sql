-- Course 6 setup — scoped datafix_user login + per-tenant grants (SQL Server).
-- The deploy user gets reader/writer on the product data (dbo) plus CREATE TABLE,
-- and OWNS a dedicated 'datafix' schema where its rollback-backup tables land.
-- Because it owns that schema, CREATE TABLE alone is enough to place the backup
-- there — no ALTER on dbo — so the user can never create, alter, or drop the
-- product's own (dbo) tables. Run once against master; USE switches context.
-- These grants are provisional: certify against your own fix and tighten.

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
-- Dedicated schema the deploy user OWNS; backup tables land here.
IF SCHEMA_ID('datafix') IS NULL
    EXEC('CREATE SCHEMA datafix AUTHORIZATION datafix_user');
GO
-- Reader/writer on the product data (dbo) — no structural rights there
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO datafix_user;
-- Create rollback-backup tables: they land in the user-owned 'datafix' schema,
-- so CREATE TABLE alone suffices (ownership covers placement; no ALTER on dbo).
GRANT CREATE TABLE TO datafix_user;
-- Ancillary stored procedures or functions the fix script may call
GRANT EXECUTE ON SCHEMA::dbo TO datafix_user;
-- tempdb access for #temp tables is implicit for any login; no grant needed.
GO

-- ── shop_tenant_b ────────────────────────────────────────────────────────────
USE shop_tenant_b;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'datafix_user')
    CREATE USER datafix_user FOR LOGIN datafix_user;
GO
IF SCHEMA_ID('datafix') IS NULL
    EXEC('CREATE SCHEMA datafix AUTHORIZATION datafix_user');
GO
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO datafix_user;
GRANT CREATE TABLE TO datafix_user;
GRANT EXECUTE ON SCHEMA::dbo TO datafix_user;
GO

-- ── shop_tenant_c ────────────────────────────────────────────────────────────
USE shop_tenant_c;
GO
IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'datafix_user')
    CREATE USER datafix_user FOR LOGIN datafix_user;
GO
IF SCHEMA_ID('datafix') IS NULL
    EXEC('CREATE SCHEMA datafix AUTHORIZATION datafix_user');
GO
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO datafix_user;
GRANT CREATE TABLE TO datafix_user;
GRANT EXECUTE ON SCHEMA::dbo TO datafix_user;
GO
