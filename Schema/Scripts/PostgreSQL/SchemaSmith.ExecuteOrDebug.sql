-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."ExecuteOrDebug"(p_Script TEXT, p_WhatIf BOOLEAN)
    LANGUAGE plpgsql
AS $$
DECLARE
    code_block TEXT;
BEGIN
    IF NULLIF(p_Script, '') IS NOT NULL THEN
        IF p_WhatIf THEN
            RAISE NOTICE '%', p_Script;
        ELSE
            code_block := '
D' || 'O $' || '$
BEGIN
' || p_Script || '
END $' || '$
';
            EXECUTE code_block;
        END IF;
    END IF;
END $$