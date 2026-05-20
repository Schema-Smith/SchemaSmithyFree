-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR ALTER PROCEDURE [{{SchemaName}}].[GetTokens]
AS
BEGIN
    SET NOCOUNT ON;
    -- Both tokens resolve at deploy time; the procedure body just carries the resolved
    -- literals so the engine treats both tokens as "consumed" (otherwise an unused token
    -- might be optimised away in some future change). Test asserts on TokenCallCounter
    -- side-effect rows, not on this proc body.
    SELECT '{{PerDbToken}}' AS PerDbToken, '{{IterationToken}}' AS IterationToken;
END;
