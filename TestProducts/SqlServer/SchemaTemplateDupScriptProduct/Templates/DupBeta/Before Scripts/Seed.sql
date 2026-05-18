-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- DupBeta template's Before Scripts/Seed.sql — identical relative path to DupAlpha's. Both
-- must produce independent tracking rows under slice 2's strict template_name semantics.
INSERT INTO [{{SchemaName}}].[DupAudit] ([Source]) VALUES (N'DupBeta-{{SchemaName}}');
