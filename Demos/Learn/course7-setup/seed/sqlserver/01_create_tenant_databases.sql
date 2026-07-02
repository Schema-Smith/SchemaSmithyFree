-- Course 7 setup: five empty tenant databases (SQL Server).
-- Distinct 'fleet_tenant_' prefix so Course 7's catalog query never picks up
-- Course 6's shop_tenant_a/b/c in the shared sandbox.
IF DB_ID('fleet_tenant_001') IS NULL CREATE DATABASE [fleet_tenant_001];
IF DB_ID('fleet_tenant_002') IS NULL CREATE DATABASE [fleet_tenant_002];
IF DB_ID('fleet_tenant_003') IS NULL CREATE DATABASE [fleet_tenant_003];
IF DB_ID('fleet_tenant_004') IS NULL CREATE DATABASE [fleet_tenant_004];
IF DB_ID('fleet_tenant_005') IS NULL CREATE DATABASE [fleet_tenant_005];
