-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

CREATE OR REPLACE FUNCTION "{{SchemaName}}".get_tokens()
RETURNS TABLE (per_db_token TEXT, iteration_token TEXT) AS $func$
BEGIN
    -- Both tokens resolve at deploy time; the function body just carries the resolved
    -- literals so the engine treats both tokens as "consumed". Test asserts on
    -- public.token_call_counter side-effect rows, not on this function body.
    RETURN QUERY SELECT '{{PerDbToken}}'::TEXT, '{{IterationToken}}'::TEXT;
END;
$func$ LANGUAGE plpgsql;
