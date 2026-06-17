-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- [ALWAYS] script: design §6.7 — runs every quench, not tracked. In a schema template the
-- script runs once per iteration per quench. MAX() is computed against a tenant-filtered
-- subset so parallel iterations from sibling tenants don't race for the same audit_id.
INSERT INTO public.shared_audit (audit_id, tenant, note)
    VALUES (
        (SELECT COALESCE(MAX(audit_id), 0) + 1 FROM public.shared_audit WHERE tenant = '{{SchemaName}}'),
        '{{SchemaName}}',
        'always-touched');
