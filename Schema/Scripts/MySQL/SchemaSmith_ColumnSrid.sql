-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP FUNCTION IF EXISTS SchemaSmith_ColumnSrid//

CREATE FUNCTION SchemaSmith_ColumnSrid(
    p_Schema VARCHAR(64),
    p_Table VARCHAR(64),
    p_Column VARCHAR(64)
) RETURNS INT
READS SQL DATA
BEGIN
    -- Returns the column's live SRID restriction (INFORMATION_SCHEMA.COLUMNS.SRS_ID), or NULL when the
    -- column carries none.
    --
    -- WHY this exists: MySQL 8.0.3+ exposes SRS_ID, but unlike SchemaSmith_IndexIsVisible's
    -- IS_VISIBLE/IGNORED divergence (both engines have SOME column, just a different name), MariaDB has
    -- no SRS_ID column at all -- see SchemaSmith_SupportsColumnSrid. A static SRS_ID reference compiled
    -- unconditionally into a shared caller (GenerateTableJson, ModifiedTableQuench) would fail to bind on
    -- MariaDB with ER_BAD_FIELD_ERROR, breaking extraction for every table, not just spatial ones.
    -- Isolating the read here, mirrored by an always-NULL MariaDb override
    -- (Scripts/MariaDb/SchemaSmith_ColumnSrid.sql), keeps the divergence out of the shared caller
    -- scripts -- same shape as SchemaSmith_IndexIsVisible / SchemaSmith_SnapshotIndexVisibility.
    --
    -- Below MySQL 8.0.3 the SRS_ID column is also absent; the early RETURN leaves the SRS_ID statement
    -- unreached -> unbound on 5.7/8.0.0-8.0.2 (column resolution is deferred to execution).
    IF SchemaSmith_ServerVersionNum() < 800 THEN
        RETURN NULL;
    END IF;

    RETURN (
        SELECT c.SRS_ID
        FROM INFORMATION_SCHEMA.COLUMNS c
        WHERE BINARY c.TABLE_SCHEMA = BINARY p_Schema
          AND BINARY c.TABLE_NAME = BINARY p_Table
          AND BINARY c.COLUMN_NAME = BINARY p_Column
    );
END //

DELIMITER ;
