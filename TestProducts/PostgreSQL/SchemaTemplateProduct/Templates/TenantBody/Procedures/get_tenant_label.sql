-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR REPLACE FUNCTION "{{SchemaName}}".get_tenant_label() RETURNS TEXT AS $func$
BEGIN
    -- The literal below is substituted from the iteration-scoped TenantLabel query token
    -- (which itself depends on {{SchemaName}}). Asserting on this string in the deployed
    -- function body proves token resolution re-runs per iteration.
    RETURN '{{TenantLabel}}';
END;
$func$ LANGUAGE plpgsql;
