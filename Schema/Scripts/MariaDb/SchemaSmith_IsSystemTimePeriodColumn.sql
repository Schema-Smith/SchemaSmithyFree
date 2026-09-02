-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP FUNCTION IF EXISTS SchemaSmith_IsSystemTimePeriodColumn//

CREATE FUNCTION SchemaSmith_IsSystemTimePeriodColumn(
    p_Schema VARCHAR(64),
    p_Table VARCHAR(64),
    p_Column VARCHAR(64)
) RETURNS TINYINT
READS SQL DATA
BEGIN
    -- MariaDb variant override of the shared MySQL function. See the MySQL base definition for why this
    -- is isolated in a function at all rather than inlined into GenerateTableJson.
    --
    -- Returns 1 for the row-start / row-end columns of a system-versioned table. Those columns are
    -- generated and maintained by the engine; SchemaSmith must not extract them as ordinary columns,
    -- because the apply path would then try to manage them and a re-deploy would attempt DDL the engine
    -- owns. SQL Server has the same exclusion for GENERATED ALWAYS AS ROW START/END (#369).
    --
    -- Only the EXPLICIT authoring form exposes them. Verified live on 11.4.12:
    --   CREATE TABLE t (...) WITH SYSTEM VERSIONING                  -> row_start/row_end are HIDDEN,
    --                                                                   absent from INFORMATION_SCHEMA.COLUMNS
    --   CREATE TABLE t (..., rs ... ROW START, re ... ROW END,
    --                   PERIOD FOR SYSTEM_TIME(rs, re)) WITH SYSTEM VERSIONING
    --                                                                -> rs/re present, EXTRA = 'STORED GENERATED',
    --                                                                   IS_SYSTEM_TIME_PERIOD_START/_END = 'YES'
    -- So the implicit form needs no exclusion and this simply returns 0 for it.
    --
    -- Version-gated on 11.4, NOT 10.3. The IS_SYSTEM_TIME_PERIOD_* columns do NOT arrive with system
    -- versioning -- verified against live servers: absent on 10.2 AND on 10.6, present on 11.4. They
    -- ship with INFORMATION_SCHEMA.PERIODS in 11.4, the same release and the same read-gap the
    -- application-time period support already documents. An earlier 10.3 guard here was asserted rather
    -- than measured, and cost every MariaDB 10.3-11.3 target an 'Unknown column' failure.
    --
    -- Column resolution is deferred to execution, so below the gate the early RETURN leaves the read
    -- unreached and therefore unbound -- the same mechanism the MySQL base definition relies on.
    --
    -- Returning 0 below 11.4 means an explicitly-declared ROW START/END column is not recognised as
    -- engine-owned there. That is the honest answer for a catalog that cannot report it, and it matches
    -- what the period reader does on the same versions: report nothing rather than guess.
    IF SchemaSmith_ServerVersionNum() < 1104 THEN
        RETURN 0;
    END IF;

    RETURN COALESCE((
        SELECT CASE WHEN c.IS_SYSTEM_TIME_PERIOD_START = 'YES'
                      OR c.IS_SYSTEM_TIME_PERIOD_END = 'YES'
                    THEN 1 ELSE 0 END
        FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE BINARY c.TABLE_SCHEMA = BINARY p_Schema
          AND BINARY c.TABLE_NAME = BINARY p_Table
          AND BINARY c.COLUMN_NAME = BINARY p_Column
    ), 0);
END //

DELIMITER ;
