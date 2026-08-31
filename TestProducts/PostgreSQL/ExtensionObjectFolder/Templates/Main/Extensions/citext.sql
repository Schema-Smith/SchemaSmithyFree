-- A PostgreSQL extension declared as an ordinary scripted object.
--
-- Object scripts run on every quench, so the script must be idempotent -- which is exactly what
-- CREATE EXTENSION IF NOT EXISTS is for. Extensions are database-scoped and are a component of no
-- table, so they are never dropped by absence: removing this file stops SchemaSmith creating the
-- extension, it does not remove one that is already installed.
CREATE EXTENSION IF NOT EXISTS citext;
