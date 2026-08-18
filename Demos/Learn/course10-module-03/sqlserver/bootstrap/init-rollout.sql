-- Bootstrap the state gate: create the per-database RolloutControl table and seed
-- every tenant's control row to 'Pending'. This mirrors the SqlServer-RollingRollout
-- demo's init-tenants.sql — the control table is OPERATIONAL STATE the DBA owns, not
-- part of the schema package being rolled out. It is established BEFORE the deploy so
-- the state-gated index's EXISTS predicate has a table to read at quench time,
-- regardless of the order SchemaSmith processes tables in.
--
-- The tenant databases already exist (stood up by course10-setup). We do NOT create
-- databases here — only the control table and its default 'Pending' row.
--
-- Initial state (before any human flips a row):
--   learn_2022 : CustomerActivityIndex = 'Pending'  -> state-gated index skipped
--   learn_2016 : CustomerActivityIndex = 'Pending'  -> state-gated index skipped
--   learn_2008 : CustomerActivityIndex = 'Pending'  -> state-gated index skipped
--
-- Part B advances ONE tenant (learn_2008) by flipping its row to 'Ready' and
-- re-deploying — see the lab README.

USE learn_2022;
IF OBJECT_ID('dbo.RolloutControl') IS NULL
  CREATE TABLE dbo.RolloutControl (
    feature VARCHAR(64) NOT NULL PRIMARY KEY,
    status  VARCHAR(16) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'CustomerActivityIndex')
  INSERT INTO dbo.RolloutControl(feature, status) VALUES ('CustomerActivityIndex', 'Pending');
GO

USE learn_2016;
IF OBJECT_ID('dbo.RolloutControl') IS NULL
  CREATE TABLE dbo.RolloutControl (
    feature VARCHAR(64) NOT NULL PRIMARY KEY,
    status  VARCHAR(16) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'CustomerActivityIndex')
  INSERT INTO dbo.RolloutControl(feature, status) VALUES ('CustomerActivityIndex', 'Pending');
GO

USE learn_2008;
IF OBJECT_ID('dbo.RolloutControl') IS NULL
  CREATE TABLE dbo.RolloutControl (
    feature VARCHAR(64) NOT NULL PRIMARY KEY,
    status  VARCHAR(16) NOT NULL
  );
IF NOT EXISTS (SELECT 1 FROM dbo.RolloutControl WHERE feature = 'CustomerActivityIndex')
  INSERT INTO dbo.RolloutControl(feature, status) VALUES ('CustomerActivityIndex', 'Pending');
GO
