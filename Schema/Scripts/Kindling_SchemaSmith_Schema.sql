﻿IF SCHEMA_ID('SchemaSmith') IS NULL
BEGIN
  EXEC('CREATE SCHEMA [SchemaSmith]')
END