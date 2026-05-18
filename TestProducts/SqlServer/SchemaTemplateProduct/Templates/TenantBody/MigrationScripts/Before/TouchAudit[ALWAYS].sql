-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- [ALWAYS] script: design §6.7 — runs every quench, not tracked. In a schema template the
-- script runs once per iteration per quench. Each invocation INSERTs a fresh row keyed by
-- ((# of prior rows for this tenant + 1), tenant). Parallel iterations from sibling tenants
-- compute MAX() against a tenant-filtered subset so they don't race for the same id.
DECLARE @NextId INT = (SELECT ISNULL(MAX([AuditID]), 0) + 1 FROM dbo.SharedAudit WHERE [Tenant] = N'{{SchemaName}}');
INSERT INTO dbo.SharedAudit ([AuditID], [Tenant], [Note])
    VALUES (@NextId, N'{{SchemaName}}', N'always-touched');
