-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP FUNCTION IF EXISTS SchemaSmith_NormalizeIndexColumns//

CREATE FUNCTION SchemaSmith_NormalizeIndexColumns(
    p_IndexColumns TEXT
) RETURNS TEXT CHARSET utf8mb4 COLLATE utf8mb4_unicode_ci
NOT DETERMINISTIC
READS SQL DATA
SQL SECURITY DEFINER
BEGIN
    -- Normalizes an index column list for comparison. Canonical per-column form, comma-separated:
    -- `ColumnName`(SubPart) DESC -- backtick-wrapped name, then (n) only when a prefix length was
    -- declared, then DESC only when the target actually stores descending key parts. Both the
    -- declared side (here) and the catalog-snapshot side (SchemaSmith_IndexOnlyQuench.sql /
    -- SchemaSmith_MissingIndexesAndConstraintsQuench.sql _SchemaSmith_IdxDetectSnap builds) must
    -- produce this exact form or the comparison never converges.
    --
    -- When the target does NOT store descending index key parts (MySQL 5.7 / MariaDB 10.2-10.7), the DESC
    -- suffix is dropped: the engine silently stores such indexes ascending, so the live catalog always
    -- reports ascending. Dropping DESC here makes the desired list match that ascending catalog, keeping the
    -- deploy idempotent (without this, a declared DESC index would be seen as modified and rebuilt every run).
    -- The 'downgraded' visibility is recorded by the index-apply procs. NOT DETERMINISTIC: reads the version.

    DECLARE v_Result TEXT DEFAULT '';
    DECLARE v_Column TEXT;
    DECLARE v_Pos INT DEFAULT 1;
    DECLARE v_Len INT;
    DECLARE v_Comma INT;
    DECLARE v_Trimmed TEXT;
    DECLARE v_IsDesc INT DEFAULT 0;
    DECLARE v_ColName TEXT;
    DECLARE v_Prefix TEXT DEFAULT '';
    DECLARE v_ParenPos INT DEFAULT 0;
    DECLARE v_SupportsDesc TINYINT DEFAULT SchemaSmith_SupportsDescendingIndex();

    IF p_IndexColumns IS NULL OR TRIM(p_IndexColumns) = '' THEN
        RETURN '';
    END IF;

    SET v_Len = CHAR_LENGTH(p_IndexColumns);

    -- Process each column
    WHILE v_Pos <= v_Len DO
        -- Find next comma
        SET v_Comma = LOCATE(',', p_IndexColumns, v_Pos);
        IF v_Comma = 0 THEN
            SET v_Comma = v_Len + 1;
        END IF;

        -- Extract column
        SET v_Column = TRIM(SUBSTRING(p_IndexColumns, v_Pos, v_Comma - v_Pos));

        -- Check for DESC suffix
        SET v_IsDesc = 0;
        IF UPPER(v_Column) LIKE '% DESC' THEN
            SET v_IsDesc = 1;
            SET v_Column = TRIM(SUBSTRING(v_Column, 1, CHAR_LENGTH(v_Column) - 5));
        ELSEIF UPPER(v_Column) LIKE '% ASC' THEN
            SET v_Column = TRIM(SUBSTRING(v_Column, 1, CHAR_LENGTH(v_Column) - 4));
        END IF;

        -- Split off a trailing prefix length, e.g. `code`(5) -> `code` + (5), BEFORE stripping
        -- backticks -- SchemaSmith_StripBacktickWrapping expects a bare/backtick-wrapped identifier
        -- and would otherwise mangle the whole `code`(5) token.
        SET v_Prefix = '';
        SET v_ParenPos = 0;
        IF RIGHT(v_Column, 1) = ')' THEN
            SET v_ParenPos = CHAR_LENGTH(v_Column) - LOCATE('(', REVERSE(v_Column)) + 1;
        END IF;
        IF v_ParenPos > 0 THEN
            SET v_Prefix = SUBSTRING(v_Column, v_ParenPos);
            SET v_Column = TRIM(SUBSTRING(v_Column, 1, v_ParenPos - 1));
        END IF;

        -- Strip existing backticks and re-wrap
        SET v_ColName = SchemaSmith_StripBacktickWrapping(v_Column);

        -- Build result
        IF v_Result != '' THEN
            SET v_Result = CONCAT(v_Result, ',');
        END IF;
        SET v_Result = CONCAT(v_Result, '`', v_ColName, '`', v_Prefix);
        IF v_IsDesc = 1 AND v_SupportsDesc = 1 THEN
            SET v_Result = CONCAT(v_Result, ' DESC');
        END IF;

        -- Move to next column
        SET v_Pos = v_Comma + 1;
    END WHILE;

    RETURN v_Result;
END//

DELIMITER ;
