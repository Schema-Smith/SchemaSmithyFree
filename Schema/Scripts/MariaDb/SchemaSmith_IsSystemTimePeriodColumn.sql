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
    -- Version-gated: the IS_SYSTEM_TIME_PERIOD_* columns arrive with system versioning in 10.3, and the
    -- supported floor is 10.2. Column resolution is deferred to execution, so on 10.2 the early RETURN
    -- leaves the read below unreached and therefore unbound -- the same mechanism the MySQL base
    -- definition relies on, and the reason this is safe to ship to the floor.
    IF SchemaSmith_ServerVersionNum() < 1003 THEN
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
