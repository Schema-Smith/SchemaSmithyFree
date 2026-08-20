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
        'AutoIncrementValue', t.AUTO_INCREMENT,
        -- Emit the sticky drop-protection marker first-class. Emitted as NULL when unset and stripped by the
        -- JSON_REMOVE pass below, so only protected tables carry "PreventDrop": true. Read from ProductOwnership. #270
        'PreventDrop', CASE WHEN EXISTS (SELECT 1 FROM SchemaSmith_ProductOwnership po
                                          WHERE po.ObjectType = 'TABLE'
                                            AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_Schema USING utf8mb4)
                                            AND CONVERT(po.ObjectName USING utf8mb4) = CONVERT(p_Table USING utf8mb4)
                                            AND COALESCE(po.PreventDrop, 0) = 1)
                            THEN TRUE ELSE NULL END
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
            -- SchemaSmith_NormalizeColumnDefault folds MariaDB's divergent COLUMN_DEFAULT reporting
            -- (literal 'NULL' marker, quoted string values, current_timestamp() with parens) to the
            -- MySQL form so extraction output — and thus a round-tripped product — matches MySQL and
            -- does not phantom-churn on re-quench. It is an identity on MySQL. Shape detection
            -- (parens, function-call, binary/hex) runs on the RAW value so it isn't disturbed.
            -- Every branch is CONVERT(... USING utf8mb4) so the CASE has a single collation: the
            -- SchemaSmith_NormalizeColumnDefault result carries utf8mb4_unicode_ci while raw
            -- COLUMN_DEFAULT / string literals carry the information_schema/connection collation, and
            -- MariaDB refuses to aggregate the mix without an explicit coercion.
            'Default', CASE
                WHEN SchemaSmith_NormalizeColumnDefault(c.COLUMN_DEFAULT) IS NULL THEN NULL
                -- Numeric types: value is always a valid literal
                WHEN c.DATA_TYPE IN ('tinyint', 'smallint', 'mediumint', 'int', 'integer', 'bigint',
                                     'float', 'double', 'decimal', 'numeric', 'bit', 'year') THEN CONVERT(c.COLUMN_DEFAULT USING utf8mb4)
                -- Expression defaults (MySQL 8.0.13+): wrapped in parentheses
                WHEN c.COLUMN_DEFAULT LIKE '(%' THEN CONVERT(c.COLUMN_DEFAULT USING utf8mb4)
                -- Function/keyword defaults (CURRENT_TIMESTAMP, CURRENT_DATE, etc.): normalize so
                -- MariaDB's current_timestamp() folds to the MySQL CURRENT_TIMESTAMP form.
                WHEN UPPER(TRIM(c.COLUMN_DEFAULT)) LIKE 'CURRENT\_%' ESCAPE '\\' THEN CONVERT(SchemaSmith_NormalizeColumnDefault(c.COLUMN_DEFAULT) USING utf8mb4)
                -- Function calls like NOW(), UUID()
                WHEN UPPER(TRIM(c.COLUMN_DEFAULT)) LIKE '%()' THEN CONVERT(c.COLUMN_DEFAULT USING utf8mb4)
                -- Binary/hex literals
                WHEN c.COLUMN_DEFAULT LIKE 'b''%' THEN CONVERT(c.COLUMN_DEFAULT USING utf8mb4)
                WHEN c.COLUMN_DEFAULT LIKE '0x%' THEN CONVERT(c.COLUMN_DEFAULT USING utf8mb4)
                -- String literals: normalize (strips MariaDB's outer quotes) then wrap consistently
                ELSE CONVERT(CONCAT('''', REPLACE(CONVERT(SchemaSmith_NormalizeColumnDefault(c.COLUMN_DEFAULT) USING utf8mb4), '''', ''''''), '''') USING utf8mb4)
            END,
            'AutoIncrement', CASE WHEN c.EXTRA LIKE '%auto_increment%' THEN TRUE ELSE FALSE END,
            -- EXTRA is a single column shared by both engines and predates the invisible-column feature,
            -- so (unlike SchemaSmith_IndexIsVisible's IS_VISIBLE/IGNORED divergence, which required a
            -- per-engine wrapper to avoid binding a column that doesn't exist below its floor) reading it
            -- here is safe on every version: below MySQL 8.0.23 / MariaDB 10.3 the INVISIBLE marker simply
            -- never appears, so the LIKE is always false there -- no version gate needed at extraction time.
            'Invisible', CASE WHEN c.EXTRA LIKE '%INVISIBLE%' THEN TRUE ELSE FALSE END,
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

    -- Get indexes (excluding FULLTEXT which are handled separately). A functional/expression key part
    -- (MySQL 8.0.13+) has NULL COLUMN_NAME and reports its text via EXPRESSION instead; that column does
    -- not exist below the floor or on MariaDB, so the branch that reads it is gated behind
    -- SchemaSmith_SupportsFunctionalIndex() as two whole statements rather than one CASE inside a single
    -- statement -- column resolution is deferred to the execution of whichever statement actually runs (see
    -- SchemaSmith_IndexIsVisible / SchemaSmith_SnapshotIndexVisibility for the same IS_VISIBLE-below-8.0
    -- shape), so the unreached branch's EXPRESSION reference is never bound on an engine that lacks it.
    -- The expression is wrapped in one extra paren pair, matching what MySQL's own SHOW CREATE TABLE
    -- renders for a functional key part -- the form a user hand-authoring the JSON would recognize.
    -- EXPRESSION also carries a charset-introducer prefix (e.g. _latin1'...', _utf8mb4'...' -- it
    -- reflects the connection charset in effect when the index was CREATEd, so it is not a fixed
    -- value) on any string literal -- the same MySQL re-serialization quirk already stripped from
    -- CHECK_CLAUSE below, generalized here to any introducer rather than an enumerated list. AND,
    -- confirmed live (unlike SHOW CREATE TABLE, which does not do this): EXPRESSION additionally
    -- backslash-escapes that literal's quotes, e.g. _latin1\'$.tags\' -- so a two-pass clean is
    -- needed: (1) turn every backslash-escaped quote into a plain quote (a targeted `\'`-to-`'`
    -- substitution via CHAR(92)/CHAR(39), NOT a blanket backslash strip -- a JSON path or literal
    -- may legitimately contain an unrelated backslash, and destroying it would corrupt the compare
    -- more subtly than the bug being fixed here), THEN (2) strip the now-plainly-quoted introducer.
    -- Stripped so a multi-valued key part's mandatory JSON-path literal (CAST(col->'$.path' AS ...
    -- ARRAY) always carries one) round-trips clean instead of leaving internal charset/escaping
    -- noise the user never typed, which would otherwise never match the declared side and rebuild
    -- forever. Applies to any functional index whose expression contains a string literal, not just
    -- a multi-valued one -- lower(`name`) (C1-1's test case) has none, which is why it never surfaced
    -- this. Applied identically at both _SchemaSmith_IdxDetectSnap builds so drift comparison converges.
    -- REGEXP_REPLACE (MySQL 8.0.4+) IS safe here despite the 5.7 floor: this whole branch only
    -- executes when SchemaSmith_SupportsFunctionalIndex() = 1 (8.0.13+), and MySQL stored-routine
    -- bodies are not semantically validated at CREATE time -- function-name resolution, like the
    -- EXPRESSION column reference above, is deferred to the execution of whichever branch actually
    -- runs, so REGEXP_REPLACE compiles fine into an unreached branch on 5.7/MariaDB. This differs
    -- from SchemaSmith_StripLeadingSelect.sql and the CHECK_CLAUSE case in ParseTableJson.sql: both
    -- of those run unconditionally on every target regardless of version, so REGEXP_REPLACE there
    -- would actually be invoked on 5.7 and fail -- an execution-time constraint, not a compile-time
    -- one, and not the situation here.
    IF SchemaSmith_SupportsFunctionalIndex() = 1 THEN
        SELECT CONCAT('[', IFNULL(GROUP_CONCAT(idx_json SEPARATOR ','), ''), ']') INTO v_indexes
        FROM (
            SELECT JSON_OBJECT(
                'Name', s.INDEX_NAME,
                'PrimaryKey', CASE WHEN s.INDEX_NAME = 'PRIMARY' THEN TRUE ELSE FALSE END,
                'Unique', CASE WHEN s.NON_UNIQUE = 0 THEN TRUE ELSE FALSE END,
                'UniqueConstraint', CASE WHEN s.INDEX_NAME = 'PRIMARY' OR s.NON_UNIQUE = 0 THEN TRUE ELSE FALSE END,
                'IndexType', s.INDEX_TYPE,
                'IndexColumns', GROUP_CONCAT(
                    CASE WHEN s.COLUMN_NAME IS NOT NULL THEN
                        CONCAT('`', s.COLUMN_NAME, '`',
                            CASE WHEN s.SUB_PART IS NOT NULL AND s.INDEX_TYPE != 'SPATIAL' THEN CONCAT('(', s.SUB_PART, ')') ELSE '' END,
                            CASE WHEN s.COLLATION = 'D' THEN ' DESC' ELSE '' END
                        )
                    ELSE
                        CONCAT('(', REGEXP_REPLACE(
                            REPLACE(s.EXPRESSION, CONCAT(CHAR(92), CHAR(39)), CHAR(39)),
                            '_[A-Za-z0-9]+''', ''''), ')',
                            CASE WHEN s.COLLATION = 'D' THEN ' DESC' ELSE '' END
                        )
                    END
                    ORDER BY s.SEQ_IN_INDEX
                    SEPARATOR ','
                ),
                'Visible', CASE WHEN SchemaSmith_IndexIsVisible(p_Schema, p_Table, s.INDEX_NAME) = 1 THEN TRUE ELSE FALSE END,
                'Comment', NULLIF(s.INDEX_COMMENT, '')
            ) AS idx_json
            FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE s.TABLE_SCHEMA = p_Schema
              AND s.TABLE_NAME = p_Table
              AND s.INDEX_TYPE != 'FULLTEXT'
            GROUP BY s.INDEX_NAME, s.NON_UNIQUE, s.INDEX_TYPE, s.INDEX_COMMENT
        ) idx_subquery;
    ELSE
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
                'Visible', CASE WHEN SchemaSmith_IndexIsVisible(p_Schema, p_Table, s.INDEX_NAME) = 1 THEN TRUE ELSE FALSE END,
                'Comment', NULLIF(s.INDEX_COMMENT, '')
            ) AS idx_json
            FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE s.TABLE_SCHEMA = p_Schema
              AND s.TABLE_NAME = p_Table
              AND s.INDEX_TYPE != 'FULLTEXT'
            GROUP BY s.INDEX_NAME, s.NON_UNIQUE, s.INDEX_TYPE, s.INDEX_COMMENT
        ) idx_subquery;
    END IF;

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

    -- Get check constraints (MySQL 8.0.16+ / MariaDB; INFORMATION_SCHEMA.CHECK_CONSTRAINTS does not
    -- exist on MySQL 5.7). MySQL binds INFORMATION_SCHEMA references at CREATE time, so the read must
    -- live only inside this dynamically-built string, executed under the SupportsCheckConstraints guard.
    IF SchemaSmith_SupportsCheckConstraints() = 1 THEN
        SET @v_ccSchema = p_Schema;
        SET @v_ccTable = p_Table;
        SET @v_ccSql = 'SELECT CONCAT(''['', IFNULL(GROUP_CONCAT(
    JSON_OBJECT(
        ''Name'', cc.CONSTRAINT_NAME,
        ''Expression'', REPLACE(REGEXP_REPLACE(cc.CHECK_CLAUSE, ''_utf8mb4|_utf8mb3|_utf8|_latin1|_binary'', ''''), ''\\\\'''''', '''''''')
    )
    SEPARATOR '',''
), ''''), '']'') INTO @v_ccResult
FROM INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
  ON cc.CONSTRAINT_SCHEMA = tc.CONSTRAINT_SCHEMA
  AND cc.CONSTRAINT_NAME = tc.CONSTRAINT_NAME
WHERE tc.TABLE_SCHEMA = @v_ccSchema
  AND tc.TABLE_NAME = @v_ccTable
  AND tc.CONSTRAINT_TYPE = ''CHECK''';
        PREPARE stmt FROM @v_ccSql;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
        SET v_check_constraints = @v_ccResult;
    ELSE
        SET v_check_constraints = '[]';
    END IF;

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

    -- Combine all into final JSON. Nest the JSON-text vars as JSON values via JSON_EXTRACT(x,'$')
    -- rather than CAST(x AS JSON): MariaDB has no native JSON type and rejects the CAST syntax,
    -- while JSON_EXTRACT(x,'$') nests identically on both engines (and yields JSON null for NULL).
    SET v_json = JSON_SET(v_json,
        '$.Columns', JSON_EXTRACT(v_columns, '$'),
        '$.Indexes', JSON_EXTRACT(v_indexes, '$'),
        '$.ForeignKeys', JSON_EXTRACT(v_foreign_keys, '$'),
        '$.CheckConstraints', JSON_EXTRACT(v_check_constraints, '$'),
        '$.FullTextIndexes', JSON_EXTRACT(v_fulltext_indexes, '$')
    );

    -- Remove null values for cleaner output
    SET v_json = JSON_REMOVE(v_json,
        CASE WHEN JSON_EXTRACT(v_json, '$.Comment') IS NULL THEN '$.Comment' ELSE '$.___dummy___' END,
        CASE WHEN JSON_EXTRACT(v_json, '$.AutoIncrementValue') IS NULL THEN '$.AutoIncrementValue' ELSE '$.___dummy___' END,
        CASE WHEN JSON_EXTRACT(v_json, '$.RowFormat') IS NULL THEN '$.RowFormat' ELSE '$.___dummy___' END,
        CASE WHEN COALESCE(JSON_TYPE(JSON_EXTRACT(v_json, '$.PreventDrop')), 'NULL') = 'NULL' THEN '$.PreventDrop' ELSE '$.___dummy___' END
    );

    SELECT v_json AS TableJson;
END //

DELIMITER ;
