-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- A schema template with NO Tables — only Before/After free-form scripts. The engine
-- must still iterate per discovered schema, substitute {{SchemaName}}, and track the
-- migration per (template, schema). Inserts into a per-tenant audit table that the
-- test creates ahead of time (since we have no Tables folder to create it via the
-- domain layer).
INSERT INTO [{{SchemaName}}].[Audit] ([Note]) VALUES (N'no-tables-marker-{{SchemaName}}');
