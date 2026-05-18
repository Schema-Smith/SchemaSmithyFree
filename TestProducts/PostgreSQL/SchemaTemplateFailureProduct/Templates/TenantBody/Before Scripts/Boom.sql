-- Deliberately fails when {{SchemaName}} resolves to 'tenant_boom'.
-- All other tenants pass through this script without error.
DO $$
BEGIN
    IF '{{SchemaName}}' = 'tenant_boom' THEN
        RAISE EXCEPTION 'Deliberate failure for schema tenant_boom (FailureIsolation test)';
    END IF;
END;
$$;
