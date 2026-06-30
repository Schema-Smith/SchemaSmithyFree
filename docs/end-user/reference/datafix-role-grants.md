# Datafix Role Grants Reference

Running a datafix through SchemaSmith means deploying under a *scoped* account — one with enough privilege to read, update, and back up data, but no ability to touch schema structure you didn't explicitly authorize. SchemaSmith itself performs no structural DDL under the datafix deployment profile; it executes the migration scripts you provide. Those scripts, however, often need targeted capabilities beyond basic reader/writer access — the most common being `CREATE TABLE` for rollback backup tables.

The grant sets below are the recommended starting point for a `datafix_user` account on each supported engine. They are scoped to the minimum required for the Course 6 lab scenario: a price-defect fix across three tenant databases (`shop_tenant_a`, `shop_tenant_b`, `shop_tenant_c`). Treat them as a baseline to tighten per environment — production accounts should carry only the grants that the specific fix has been proven to need.

> **Provisional:** Treat these as the minimal baseline the Course 6 lab exercises across all three engines. Certify them against your own fix and environment — any grant a specific datafix doesn't actually exercise should be removed.

---

## SQL Server

SQL Server separates server identity from database identity. One `LOGIN` covers authentication at the instance level; a `USER` inside each database maps that login to a database-level principal. You create the login once and repeat the `USER` + `GRANT` block for each tenant.

```sql
-- Server-level login (run against master)
CREATE LOGIN datafix_user WITH PASSWORD = 'DataFix!Demo123';

-- Repeat the block below in each tenant database: shop_tenant_a, shop_tenant_b, shop_tenant_c
USE shop_tenant_a;   -- substitute shop_tenant_b, shop_tenant_c for the other two
GO
CREATE USER datafix_user FOR LOGIN datafix_user;

-- Reader/writer on all objects in dbo (the default schema for this database)
GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo TO datafix_user;

-- Backup table: both grants are required together (see note below)
GRANT CREATE TABLE               TO datafix_user;
GRANT ALTER        ON SCHEMA::dbo TO datafix_user;

-- Ancillary stored procedures and functions the fix may call
GRANT EXECUTE      ON SCHEMA::dbo TO datafix_user;
GO
```

**`CREATE TABLE` + `ALTER ON SCHEMA::dbo` — why both are required.** `GRANT CREATE TABLE` authorizes the DDL statement itself, but SQL Server still needs to know *where* to place the table. Without `GRANT ALTER ON SCHEMA::dbo`, the engine rejects placement even though the CREATE TABLE statement is technically permitted: the user cannot modify the schema's ownership chain to accommodate the new object. The two grants are a paired unit for backup-table creation — neither is sufficient alone.

**tempdb access** is implicit for any authenticated login; no explicit grant is needed for `#temp` tables.

---

## PostgreSQL

PostgreSQL security is built around cluster-level roles rather than per-database logins. You create one `ROLE` with `LOGIN` privilege, then grant it access to each database and the objects within. Because grants on `ALL TABLES` only cover tables that exist at grant time, any tables created *after* the grant (including the backup table that the datafix script creates) are owned by `datafix_user` itself — so no additional grant is needed for tables the role creates.

```sql
-- Cluster-level role (run connected to postgres or any maintenance database)
CREATE ROLE datafix_user LOGIN PASSWORD 'DataFix!Demo123';

-- Repeat the block below for each tenant database.
-- In psql, use \connect to switch; in a non-interactive context, open a new
-- connection targeting each database.

\connect shop_tenant_a   -- substitute shop_tenant_b, shop_tenant_c for the other two

-- Allow the role to open a connection to this database
GRANT CONNECT   ON DATABASE shop_tenant_a TO datafix_user;

-- Temp space: allows CREATE TEMPORARY TABLE within a session on this database
GRANT TEMPORARY ON DATABASE shop_tenant_a TO datafix_user;

-- Schema access: USAGE to resolve object references; CREATE to place the backup table in public
GRANT USAGE, CREATE ON SCHEMA public TO datafix_user;

-- Reader/writer on all existing tables in public
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO datafix_user;

-- Ancillary functions and procedures the fix may call
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO datafix_user;
```

---

## MySQL

In MySQL a schema and a database are the same construct, so there is no separate schema-level grant layer. Tenant isolation maps directly to per-database grants on `shop_tenant_a.*`, `shop_tenant_b.*`, and `shop_tenant_c.*`. The `CREATE` privilege in the `.*` wildcard covers both `CREATE TABLE` (permanent tables) and is paired with `CREATE TEMPORARY TABLES` as a separate privilege for temporary tables.

```sql
-- User account (% means any host; tighten the host specifier in production)
CREATE USER IF NOT EXISTS 'datafix_user'@'%' IDENTIFIED BY 'DataFix!Demo123';

-- Repeat for each tenant database: shop_tenant_a, shop_tenant_b, shop_tenant_c
GRANT SELECT, INSERT, UPDATE, CREATE ON `shop_tenant_a`.* TO 'datafix_user'@'%';
-- SELECT / INSERT / UPDATE: reader/writer on existing data
-- CREATE: backup table creation (schema==database, so no separate schema-level grant needed)

GRANT CREATE TEMPORARY TABLES ON `shop_tenant_a`.* TO 'datafix_user'@'%';
-- Temp space: CREATE TEMPORARY TABLES is a distinct privilege from CREATE in MySQL

GRANT EXECUTE ON `shop_tenant_a`.* TO 'datafix_user'@'%';
-- Ancillary stored procedures and functions the fix may call

FLUSH PRIVILEGES;
```

---

## Privilege summary across engines

| Capability | SQL Server | PostgreSQL | MySQL |
|---|---|---|---|
| Reader/writer on data | `GRANT SELECT, INSERT, UPDATE ON SCHEMA::dbo` | `GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public` | `GRANT SELECT, INSERT, UPDATE ON db.*` |
| Backup table creation | `GRANT CREATE TABLE` + `GRANT ALTER ON SCHEMA::dbo` (paired — both required) | `GRANT USAGE, CREATE ON SCHEMA public` | `GRANT CREATE ON db.*` |
| Temp space | Implicit for authenticated logins | `GRANT TEMPORARY ON DATABASE` | `GRANT CREATE TEMPORARY TABLES ON db.*` |
| Execute ancillary routines | `GRANT EXECUTE ON SCHEMA::dbo` | `GRANT EXECUTE ON ALL FUNCTIONS/PROCEDURES IN SCHEMA public` | `GRANT EXECUTE ON db.*` |
