-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
IF SCHEMA_ID('SchemaSmith') IS NULL

IF SCHEMA_ID('SchemaSmith') IS NULL
BEGIN
  EXEC('CREATE SCHEMA [SchemaSmith]')
END