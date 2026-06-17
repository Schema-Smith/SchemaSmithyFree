CREATE OR REPLACE TRIGGER trg_customers_audit_last_modified
    BEFORE UPDATE ON "{{SchemaName}}".customers
    FOR EACH ROW
    EXECUTE FUNCTION "{{SchemaName}}".customers_audit_last_modified();
