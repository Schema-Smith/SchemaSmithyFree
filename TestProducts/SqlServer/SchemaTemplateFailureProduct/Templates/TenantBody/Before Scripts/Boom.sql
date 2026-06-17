-- Deliberately fails when {{SchemaName}} resolves to 'tenant_boom'.
-- All other tenants pass through this script without error.
IF '{{SchemaName}}' = 'tenant_boom'
    RAISERROR('Deliberate failure for schema tenant_boom (FailureIsolation test)', 16, 1);
