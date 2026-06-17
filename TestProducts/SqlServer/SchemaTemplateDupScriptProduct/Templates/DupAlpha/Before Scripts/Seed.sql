-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- DupAlpha template's Before Scripts/Seed.sql. The DupBeta template has a file at the
-- IDENTICAL relative path. Slice 2's strict template_name on writes/deletes must keep
-- the two tracking rows distinct.
INSERT INTO [{{SchemaName}}].[DupAudit] ([Source]) VALUES (N'DupAlpha-{{SchemaName}}');
