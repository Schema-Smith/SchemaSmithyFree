-- Bootstrap the demo: three tenant databases, each with a RolloutControl row.
-- This simulates the "DBA flips a row from Pending to Ready" surface that
-- governs whether the heavy NCCI build runs in this maintenance window.
--
-- Demo initial state:
--   Tenant_A: RolloutControl row in 'Ready'   -> SchemaQuench will build the NCCI
--   Tenant_B: RolloutControl row in 'Pending' -> SchemaQuench will skip the NCCI
--   Tenant_C: RolloutControl row in 'Pending' -> SchemaQuench will skip the NCCI

IF DB_ID('Tenant_A') IS NULL CREATE DATABASE Tenant_A;
IF DB_ID('Tenant_B') IS NULL CREATE DATABASE Tenant_B;
IF DB_ID('Tenant_C') IS NULL CREATE DATABASE Tenant_C;
GO

USE Tenant_A;
IF OBJECT_ID('dbo.RolloutControl') IS NULL
  CREATE TABLE dbo.RolloutControl (
    feature VARCHAR(64) NOT NULL PRIMARY KEY,
    status  VARCHAR(16) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'OrderHistoryColumnstore')
  INSERT INTO dbo.RolloutControl(feature, status) VALUES ('OrderHistoryColumnstore', 'Ready');
GO

USE Tenant_B;
IF OBJECT_ID('dbo.RolloutControl') IS NULL
  CREATE TABLE dbo.RolloutControl (
    feature VARCHAR(64) NOT NULL PRIMARY KEY,
    status  VARCHAR(16) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'OrderHistoryColumnstore')
  INSERT INTO dbo.RolloutControl(feature, status) VALUES ('OrderHistoryColumnstore', 'Pending');
GO

USE Tenant_C;
IF OBJECT_ID('dbo.RolloutControl') IS NULL
  CREATE TABLE dbo.RolloutControl (
    feature VARCHAR(64) NOT NULL PRIMARY KEY,
    status  VARCHAR(16) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'OrderHistoryColumnstore')
  INSERT INTO dbo.RolloutControl(feature, status) VALUES ('OrderHistoryColumnstore', 'Pending');
GO
