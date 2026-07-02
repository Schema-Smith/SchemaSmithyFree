-- Course 7 setup: five empty tenant databases (PostgreSQL).
-- PostgreSQL has no CREATE DATABASE IF NOT EXISTS; generate the CREATE statements
-- for the missing tenants and execute them with \gexec. Discovered by Course 7's
-- pg_database catalog query on the 'fleet_tenant_' prefix.
SELECT format('CREATE DATABASE %I', d)
FROM (VALUES ('fleet_tenant_001'),('fleet_tenant_002'),('fleet_tenant_003'),
             ('fleet_tenant_004'),('fleet_tenant_005')) AS t(d)
WHERE NOT EXISTS (SELECT 1 FROM pg_database WHERE datname = d)
\gexec
