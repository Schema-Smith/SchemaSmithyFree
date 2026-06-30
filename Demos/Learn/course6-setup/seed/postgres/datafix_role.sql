-- Course 6 setup — scoped datafix_user role + per-tenant grants (PostgreSQL).
-- The deploy user gets reader/writer on the product data (public) and OWNS a
-- dedicated 'datafix' schema where its rollback-backup tables land. It has NO
-- CREATE on public, so it can only create within the schema it owns — it can
-- neither add to nor drop the product's own (public) tables. Connect to the
-- postgres maintenance database first; the \connect meta-commands switch DBs.
-- These grants are provisional: certify against your own fix and tighten.

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
GRANT CONNECT   ON DATABASE shop_tenant_a TO datafix_user;
GRANT TEMPORARY ON DATABASE shop_tenant_a TO datafix_user;
-- Read/write the product data, but NO create rights in public (no structural rights there)
GRANT USAGE ON SCHEMA public TO datafix_user;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO datafix_user;
-- Dedicated schema the deploy user OWNS — backup tables go here (create within owned schema)
CREATE SCHEMA IF NOT EXISTS datafix AUTHORIZATION datafix_user;
-- Ancillary functions or procedures the fix script may call
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO datafix_user;

-- ── shop_tenant_b ────────────────────────────────────────────────────────────
\connect shop_tenant_b
GRANT CONNECT   ON DATABASE shop_tenant_b TO datafix_user;
GRANT TEMPORARY ON DATABASE shop_tenant_b TO datafix_user;
GRANT USAGE ON SCHEMA public TO datafix_user;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO datafix_user;
CREATE SCHEMA IF NOT EXISTS datafix AUTHORIZATION datafix_user;
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO datafix_user;

-- ── shop_tenant_c ────────────────────────────────────────────────────────────
\connect shop_tenant_c
GRANT CONNECT   ON DATABASE shop_tenant_c TO datafix_user;
GRANT TEMPORARY ON DATABASE shop_tenant_c TO datafix_user;
GRANT USAGE ON SCHEMA public TO datafix_user;
GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA public TO datafix_user;
CREATE SCHEMA IF NOT EXISTS datafix AUTHORIZATION datafix_user;
GRANT EXECUTE ON ALL FUNCTIONS  IN SCHEMA public TO datafix_user;
GRANT EXECUTE ON ALL PROCEDURES IN SCHEMA public TO datafix_user;
