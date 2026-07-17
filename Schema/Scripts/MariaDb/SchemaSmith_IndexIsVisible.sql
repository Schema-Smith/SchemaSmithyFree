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
    -- MariaDb variant override of the shared MySQL function. MariaDB has no IS_VISIBLE column on
    -- INFORMATION_SCHEMA.STATISTICS; it exposes the inverted IGNORED column ('NO' = visible,
    -- 'YES' = ignored/invisible). This override is the whole reason the divergence is isolated to
    -- one small function instead of forking the three large caller scripts. See the MySQL base
    -- definition for the full rationale.

    RETURN (
        SELECT CASE WHEN MAX(s.IGNORED) = 'NO' THEN 1 ELSE 0 END
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_Schema
          AND BINARY s.TABLE_NAME = BINARY p_Table
          AND BINARY s.INDEX_NAME = BINARY p_Index
    );
END //

DELIMITER ;
