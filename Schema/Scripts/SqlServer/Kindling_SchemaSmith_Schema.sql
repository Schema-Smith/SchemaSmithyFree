-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF SCHEMA_ID('SchemaSmith') IS NULL
BEGIN
  EXEC('CREATE SCHEMA [SchemaSmith]')
END