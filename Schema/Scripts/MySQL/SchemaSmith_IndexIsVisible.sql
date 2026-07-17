-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP FUNCTION IF EXISTS SchemaSmith_IndexIsVisible//

CREATE FUNCTION SchemaSmith_IndexIsVisible(
    p_Schema VARCHAR(64),
    p_Table VARCHAR(64),
    p_Index VARCHAR(64)
) RETURNS TINYINT
READS SQL DATA
BEGIN
    -- Returns 1 if the named index is visible to the optimizer, 0 if invisible.
    --
    -- WHY this exists: MySQL exposes index visibility via INFORMATION_SCHEMA.STATISTICS.IS_VISIBLE
    -- ('YES'/'NO'), a column that does not exist on MariaDB. MariaDB instead exposes the inverted
    -- IGNORED column ('NO' = visible). The callers (GenerateTableJson, IndexOnlyQuench,
    -- MissingIndexesAndConstraintsQuench) are shared across the MySQL/MariaDb variant family, so the
    -- column-name divergence is isolated here: this MySQL definition reads IS_VISIBLE; the
    -- Scripts/MariaDb override reads IGNORED. IS_VISIBLE is uniform across an index's STATISTICS rows,
    -- so MAX collapses the per-column rows to one value.

    RETURN (
        SELECT CASE WHEN MAX(s.IS_VISIBLE) = 'YES' THEN 1 ELSE 0 END
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_Schema
          AND BINARY s.TABLE_NAME = BINARY p_Table
          AND BINARY s.INDEX_NAME = BINARY p_Index
    );
END //

DELIMITER ;
