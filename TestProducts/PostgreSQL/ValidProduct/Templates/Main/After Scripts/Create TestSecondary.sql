-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Idempotent: PostgreSQL has no CREATE DATABASE IF NOT EXISTS, so drop first. Keeps this after-script
-- re-runnable across repeated test runs against a persistent server (the fixture doesn't own this
-- fixed-name database, so it isn't dropped in teardown).
DROP DATABASE IF EXISTS "TestSecondary";
CREATE DATABASE "TestSecondary";
