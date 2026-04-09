-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- GenerateTableJSON: Extracts comprehensive table metadata as JSON
-- Usage: CALL SchemaSmith_GenerateTableJSON('database_name', 'table_name');

DROP PROCEDURE IF EXISTS `SchemaSmith_GenerateTableJSON`;

DELIMITER //

CREATE PROCEDURE `SchemaSmith_GenerateTableJSON`(
    IN p_Schema VARCHAR(200),
    IN p_Table VARCHAR(200)
)
BEGIN
    DECLARE v_json LONGTEXT;
    DECLARE v_columns LONGTEXT;
    DECLARE v_indexes LONGTEXT;
    DECLARE v_foreign_keys LONGTEXT;
    DECLARE v_check_constraints LONGTEXT;
    DECLARE v_fulltext_indexes LONGTEXT;

    -- Set session variables for proper GROUP_CONCAT handling
    SET SESSION group_concat_max_len = 1000000;

    -- Get table metadata
    SELECT JSON_OBJECT(
        'Name', CONCAT('`', t.TABLE_NAME, '`'),
        'Engine', t.ENGINE,
        'RowFormat', t.ROW_FORMAT,
        'CharacterSet', SUBSTRING_INDEX(t.TABLE_COLLATION, '_', 1),
        'Collation', t.TABLE_COLLATION,
        'Comment', NULLIF(t.TABLE_COMMENT, ''),
        'AutoIncrementValue', t.AUTO_INCREMENT
    ) INTO v_json
    FROM INFORMATION_SCHEMA.TABLES t
    WHERE t.TABLE_SCHEMA = p_Schema
      AND t.TABLE_NAME = p_Table
      AND t.TABLE_TYPE = 'BASE TABLE';

    -- Get columns
    SELECT CONCAT('[', GROUP_CONCAT(
        JSON_OBJECT(
            'Name', CONCAT('`', c.COLUMN_NAME, '`'),
            'DataType', c.COLUMN_TYPE,
            'Nullable', CASE WHEN c.IS_NULLABLE = 'YES' THEN TRUE ELSE FALSE END,
            'Default', CASE
                WHEN c.COLUMN_DEFAULT IS NULL THEN NULL
                -- Numeric types: value is always a valid literal
                WHEN c.DATA_TYPE IN ('tinyint', 'smallint', 'mediumint', 'int', 'integer', 'bigint',
                                     'float', 'double', 'decimal', 'numeric', 'bit', 'year') THEN c.COLUMN_DEFAULT
                -- Expression defaults (MySQL 8.0.13+): wrapped in parentheses
                WHEN c.COLUMN_DEFAULT LIKE '(%' THEN c.COLUMN_DEFAULT
                -- Function/keyword defaults (CURRENT_TIMESTAMP, CURRENT_DATE, etc.)
                WHEN UPPER(TRIM(c.COLUMN_DEFAULT)) LIKE 'CURRENT\_%' ESCAPE '\\' THEN c.COLUMN_DEFAULT
                -- Function calls like NOW(), UUID()
                WHEN UPPER(TRIM(c.COLUMN_DEFAULT)) LIKE '%()' THEN c.COLUMN_DEFAULT
                -- Binary/hex literals
                WHEN c.COLUMN_DEFAULT LIKE 'b''%' THEN c.COLUMN_DEFAULT
                WHEN c.COLUMN_DEFAULT LIKE '0x%' THEN c.COLUMN_DEFAULT
                -- String literals: wrap in single quotes
                ELSE CONCAT('''', REPLACE(c.COLUMN_DEFAULT, '''', ''''''), '''')
            END,
            'AutoIncrement', CASE WHEN c.EXTRA LIKE '%auto_increment%' THEN TRUE ELSE FALSE END,
            'Generated', CASE
                WHEN c.EXTRA LIKE '%VIRTUAL GENERATED%' THEN 'VIRTUAL'
                WHEN c.EXTRA LIKE '%STORED GENERATED%' THEN 'STORED'
                ELSE NULL
            END,
            'GenerationExpression', NULLIF(c.GENERATION_EXPRESSION, ''),
            'CharacterSet', c.CHARACTER_SET_NAME,
            'Collation', CASE
                WHEN c.COLLATION_NAME = (SELECT TABLE_COLLATION FROM INFORMATION_SCHEMA.TABLES
                                         WHERE TABLE_SCHEMA = p_Schema AND TABLE_NAME = p_Table)
                THEN NULL  -- Don't include if same as table default
                ELSE c.COLLATION_NAME
            END,
            'Comment', NULLIF(c.COLUMN_COMMENT, '')
        )
        ORDER BY c.ORDINAL_POSITION
        SEPARATOR ','
    ), ']') INTO v_columns
    FROM INFORMATION_SCHEMA.COLUMNS c
    WHERE c.TABLE_SCHEMA = p_Schema
      AND c.TABLE_NAME = p_Table;

    -- Get indexes (excluding FULLTEXT which are handled separately)
    SELECT CONCAT('[', IFNULL(GROUP_CONCAT(idx_json SEPARATOR ','), ''), ']') INTO v_indexes
    FROM (
        SELECT JSON_OBJECT(
            'Name', s.INDEX_NAME,
            'PrimaryKey', CASE WHEN s.INDEX_NAME = 'PRIMARY' THEN TRUE ELSE FALSE END,
            'Unique', CASE WHEN s.NON_UNIQUE = 0 THEN TRUE ELSE FALSE END,
            'UniqueConstraint', CASE WHEN s.INDEX_NAME = 'PRIMARY' OR s.NON_UNIQUE = 0 THEN TRUE ELSE FALSE END,
            'IndexType', s.INDEX_TYPE,
            'IndexColumns', GROUP_CONCAT(
                CONCAT('`', s.COLUMN_NAME, '`',
                    CASE WHEN s.SUB_PART IS NOT NULL AND s.INDEX_TYPE != 'SPATIAL' THEN CONCAT('(', s.SUB_PART, ')') ELSE '' END,
                    CASE WHEN s.COLLATION = 'D' THEN ' DESC' ELSE '' END
                )
                ORDER BY s.SEQ_IN_INDEX
                SEPARATOR ','
            ),
            'Visible', CASE WHEN s.IS_VISIBLE = 'YES' THEN TRUE ELSE FALSE END,
            'Comment', NULLIF(s.INDEX_COMMENT, '')
        ) AS idx_json
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE s.TABLE_SCHEMA = p_Schema
          AND s.TABLE_NAME = p_Table
          AND s.INDEX_TYPE != 'FULLTEXT'
        GROUP BY s.INDEX_NAME, s.NON_UNIQUE, s.INDEX_TYPE, s.IS_VISIBLE, s.INDEX_COMMENT
    ) idx_subquery;

    -- Get foreign keys
    SELECT CONCAT('[', IFNULL(GROUP_CONCAT(fk_json SEPARATOR ','), ''), ']') INTO v_foreign_keys
    FROM (
        SELECT JSON_OBJECT(
            'Name', tc.CONSTRAINT_NAME,
            'Columns', (
                SELECT GROUP_CONCAT(CONCAT('`', kcu2.COLUMN_NAME, '`') ORDER BY kcu2.ORDINAL_POSITION SEPARATOR ',')
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu2
                WHERE kcu2.CONSTRAINT_SCHEMA = p_Schema
                  AND kcu2.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                  AND kcu2.TABLE_NAME = p_Table
            ),
            'RelatedTableSchema', CASE
                WHEN rc.UNIQUE_CONSTRAINT_SCHEMA = p_Schema THEN ''
                ELSE rc.UNIQUE_CONSTRAINT_SCHEMA
            END,
            'RelatedTable', CONCAT('`', rc.REFERENCED_TABLE_NAME, '`'),
            'RelatedColumns', (
                SELECT GROUP_CONCAT(CONCAT('`', kcu3.REFERENCED_COLUMN_NAME, '`') ORDER BY kcu3.ORDINAL_POSITION SEPARATOR ',')
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu3
                WHERE kcu3.CONSTRAINT_SCHEMA = p_Schema
                  AND kcu3.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
                  AND kcu3.TABLE_NAME = p_Table
            ),
            'DeleteAction', rc.DELETE_RULE,
            'UpdateAction', rc.UPDATE_RULE
        ) AS fk_json
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        JOIN INFORMATION_SCHEMA.REFERENTIAL_CONSTRAINTS rc
          ON tc.CONSTRAINT_SCHEMA = rc.CONSTRAINT_SCHEMA
          AND tc.CONSTRAINT_NAME = rc.CONSTRAINT_NAME
        WHERE tc.TABLE_SCHEMA = p_Schema
          AND tc.TABLE_NAME = p_Table
          AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
    ) fk_subquery;

    -- Get check constraints (MySQL 8.0.16+)
    SELECT CONCAT('[', IFNULL(GROUP_CONCAT(
        JSON_OBJECT(
            'Name', cc.CONSTRAINT_NAME,
            'Expression', REPLACE(REGEXP_REPLACE(cc.CHECK_CLAUSE, '_utf8mb4|_utf8|_latin1|_binary', ''), '\\''', '''')
        )
        SEPARATOR ','
    ), ''), ']') INTO v_check_constraints
    FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
    JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
      ON cc.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
      AND cc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
    WHERE tc.TABLE_SCHEMA = p_Schema
      AND tc.TABLE_NAME = p_Table
      AND tc.CONSTRAINT_TYPE = 'CHECK';

    -- Get fulltext indexes
    SELECT CONCAT('[', IFNULL(GROUP_CONCAT(ft_json SEPARATOR ','), ''), ']') INTO v_fulltext_indexes
    FROM (
        SELECT JSON_OBJECT(
            'Name', s.INDEX_NAME,
            'Columns', GROUP_CONCAT(CONCAT('`', s.COLUMN_NAME, '`') ORDER BY s.SEQ_IN_INDEX SEPARATOR ','),
            'Comment', NULLIF(MAX(s.INDEX_COMMENT), '')
        ) AS ft_json
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE s.TABLE_SCHEMA = p_Schema
          AND s.TABLE_NAME = p_Table
          AND s.INDEX_TYPE = 'FULLTEXT'
        GROUP BY s.INDEX_NAME
    ) ft_subquery;

    -- Combine all into final JSON
    SET v_json = JSON_SET(v_json,
        '$.Columns', CAST(v_columns AS JSON),
        '$.Indexes', CAST(v_indexes AS JSON),
        '$.ForeignKeys', CAST(v_foreign_keys AS JSON),
        '$.CheckConstraints', CAST(v_check_constraints AS JSON),
        '$.FullTextIndexes', CAST(v_fulltext_indexes AS JSON)
    );

    -- Remove null values for cleaner output
    SET v_json = JSON_REMOVE(v_json,
        CASE WHEN JSON_EXTRACT(v_json, '$.Comment') IS NULL THEN '$.Comment' ELSE '$.___dummy___' END,
        CASE WHEN JSON_EXTRACT(v_json, '$.AutoIncrementValue') IS NULL THEN '$.AutoIncrementValue' ELSE '$.___dummy___' END,
        CASE WHEN JSON_EXTRACT(v_json, '$.RowFormat') IS NULL THEN '$.RowFormat' ELSE '$.___dummy___' END
    );

    SELECT v_json AS TableJson;
END //

DELIMITER ;
