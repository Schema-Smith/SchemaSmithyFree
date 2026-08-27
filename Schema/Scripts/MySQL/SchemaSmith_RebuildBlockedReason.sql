-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP FUNCTION IF EXISTS SchemaSmith_RebuildBlockedReason//

CREATE FUNCTION SchemaSmith_RebuildBlockedReason(
    p_Schema VARCHAR(64),
    p_Table VARCHAR(64)
) RETURNS VARCHAR(255)
READS SQL DATA
BEGIN
    -- Answers "why can this table NOT be rebuilt?" -- a short reason naming the blocking state, or NULL when
    -- a rebuild (shadow-copy-and-swap) is safe.
    --
    -- THE EMPTY BODY IS THE ANSWER, NOT AN OMISSION. MySQL has none of the states a rebuild would silently
    -- destroy: no system-versioned tables, no application-time periods, no table-level Change Data Capture or
    -- Change Tracking, and its replication is statement/row streaming off the binlog rather than a per-table
    -- article whose identity a swap would break. There is nothing here to detect, so a rebuild is always
    -- permitted and this always returns NULL.
    --
    -- DO NOT "fix" this by copying the MariaDb override's body (Scripts/MariaDb/SchemaSmith_RebuildBlockedReason.sql).
    -- MariaDB genuinely has system-versioned tables and application-time periods; MySQL does not, and its
    -- INFORMATION_SCHEMA has no PERIODS table -- referencing one is ER_UNKNOWN_TABLE (1109) at CREATE time,
    -- which would fail the entire kindle rather than one query. Same shape as SchemaSmith_ColumnSrid, where
    -- the engine that lacks the catalog object simply returns nothing.
    --
    -- The parameters are accepted only so the signature callers rely on is identical to the MariaDb override.
    RETURN NULL;
END //

DELIMITER ;
