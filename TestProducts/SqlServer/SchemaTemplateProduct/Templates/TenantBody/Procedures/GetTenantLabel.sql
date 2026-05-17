-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR ALTER PROCEDURE [{{SchemaName}}].[GetTenantLabel]
AS
BEGIN
    SET NOCOUNT ON;
    -- The literal below is substituted from the iteration-scoped TenantLabel query token
    -- (which itself depends on {{SchemaName}}). Asserting on this string in the deployed
    -- procedure body proves token resolution re-runs per iteration.
    SELECT '{{TenantLabel}}' AS TenantLabel;
END;
