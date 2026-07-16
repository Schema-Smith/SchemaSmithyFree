-- Course 6 setup — scoped datafix_user account + per-tenant grants (MySQL).
-- In MySQL a schema and a database are the same thing, so per-tenant grants
-- target each shop_tenant_* database directly. Run as root or any account
-- with GRANT OPTION on the target databases.
-- These grants are provisional: certify against your own fix and tighten.

-- Create the user account (idempotent guard; CREATE USER IF NOT EXISTS requires MySQL 5.7.6+)
CREATE USER IF NOT EXISTS 'datafix_user'@'%' IDENTIFIED BY 'DataFix!Demo123';

-- ── shop_tenant_a ────────────────────────────────────────────────────────────
-- Reader/writer on all tables; CREATE covers the backup table (schema == database)
GRANT SELECT, INSERT, UPDATE, CREATE ON `shop_tenant_a`.* TO 'datafix_user'@'%';

-- Temp space: allows CREATE TEMPORARY TABLE in this database context
GRANT CREATE TEMPORARY TABLES         ON `shop_tenant_a`.* TO 'datafix_user'@'%';

-- Ancillary stored procedures or functions the fix script may call
GRANT EXECUTE                          ON `shop_tenant_a`.* TO 'datafix_user'@'%';

-- ── shop_tenant_b ────────────────────────────────────────────────────────────
GRANT SELECT, INSERT, UPDATE, CREATE  ON `shop_tenant_b`.* TO 'datafix_user'@'%';
GRANT CREATE TEMPORARY TABLES         ON `shop_tenant_b`.* TO 'datafix_user'@'%';
GRANT EXECUTE                          ON `shop_tenant_b`.* TO 'datafix_user'@'%';

-- ── shop_tenant_c ────────────────────────────────────────────────────────────
GRANT SELECT, INSERT, UPDATE, CREATE  ON `shop_tenant_c`.* TO 'datafix_user'@'%';
GRANT CREATE TEMPORARY TABLES         ON `shop_tenant_c`.* TO 'datafix_user'@'%';
GRANT EXECUTE                          ON `shop_tenant_c`.* TO 'datafix_user'@'%';

FLUSH PRIVILEGES;
