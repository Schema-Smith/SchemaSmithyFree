-- Bootstrap the control database the PostgreSQL demo quenches connect to. Run
-- connected to the 'postgres' maintenance database. The Initialize template in
-- each product connects to TestMain and issues CREATE DATABASE for the product
-- DB, so TestMain only needs to exist (empty) and be stamped as helper-owned.
SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = 'TestMain' AND pid <> pg_backend_pid();
DROP DATABASE IF EXISTS "TestMain";
CREATE DATABASE "TestMain";
COMMENT ON DATABASE "TestMain" IS 'SchemaSmith_DemoProvisioned';
