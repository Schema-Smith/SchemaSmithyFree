-- Course 6 setup — scoped datafix_user role + per-tenant grants (PostgreSQL).
-- Creates one cluster-level role and grants it the minimal privilege set in
-- each of the three tenant databases. Connect to the postgres maintenance
-- database first (or any superuser session); the \connect meta-commands switch
-- databases as the script progresses.
-- These grants are provisional: Task 4 certification may tighten the set.

-- Create the cluster-level role (idempotent guard)
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'datafix_user') THEN
        CREATE ROLE datafix_user LOGIN PASSWORD 'DataFix!Demo123';
    END IF;
END
$$;

-- ── shop_tenant_a ────────────────────────────────────────────────────────────
\connect shop_tenant_a

-- Allow the role to open a connection to this database
GRANT CONNECT   ON DATABASE shop_tenant_a TO datafix_user;

-- Temp space: allows CREATE TEMPORARY TABLE inside this database session
GRANT TEMPORARY ON DATABASE shop_tenant_a TO datafix_user;

-- Schema access: USAGE to see objects; CREATE to place the backup table in public
GRANT USAGE, CREATE ON SCHEMA public TO datafix_user;

-- Reader/writer on existing tables (tables created after this grant need
-- DEFAULT PRIVILEGES or an explicit GRANT if the datafix user did not create them)
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO datafix_user;

-- Ancillary functions or procedures the fix script may call
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO datafix_user;

-- ── shop_tenant_b ────────────────────────────────────────────────────────────
\connect shop_tenant_b

GRANT CONNECT   ON DATABASE shop_tenant_b TO datafix_user;
GRANT TEMPORARY ON DATABASE shop_tenant_b TO datafix_user;
GRANT USAGE, CREATE ON SCHEMA public TO datafix_user;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO datafix_user;

-- ── shop_tenant_c ────────────────────────────────────────────────────────────
\connect shop_tenant_c

GRANT CONNECT   ON DATABASE shop_tenant_c TO datafix_user;
GRANT TEMPORARY ON DATABASE shop_tenant_c TO datafix_user;
GRANT USAGE, CREATE ON SCHEMA public TO datafix_user;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO datafix_user;
