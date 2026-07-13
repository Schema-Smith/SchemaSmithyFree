-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_ModifiedTableQuench//

CREATE PROCEDURE SchemaSmith_ModifiedTableQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropTablesRemovedFromProduct TINYINT,
    IN p_DropColumnsRemovedFromProduct TINYINT,
    IN p_DropCheckConstraintsRemovedFromProduct TINYINT,
    IN p_DropExcludeConstraintsRemovedFromProduct TINYINT,
    IN p_DropStatisticsRemovedFromProduct TINYINT,
    IN p_CaptureWouldDrop TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure modifies existing tables to match the JSON definitions.
    -- It reads from the _SchemaSmith_Tables and _SchemaSmith_Columns temp tables.
    --
    -- Execution order:
    -- 0. Ownership validation (error if table owned by another product)
    -- 1. Table renames (when OldName is set and old table exists)
    -- 2. Column renames (when OldName is set and old column exists)
    -- 3. Column modifications (type, nullable, etc.)
    -- 4. Engine changes
    -- 5. Collation changes
    -- 6. Row format changes
    -- 7. Auto-increment seed changes
    -- 8. ProductOwnership updates
    -- 9. Drop tables removed from product (if enabled)

    DECLARE v_ConflictingTable VARCHAR(128);
    DECLARE v_ConflictingOwner VARCHAR(100);

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN ModifiedTableQuench');

    -- Folded multi-clause ALTER/RENAME statements below can GROUP_CONCAT many clauses for a
    -- wide table; raise the session limit so long clause lists aren't silently truncated.
    SET SESSION group_concat_max_len = 1000000;

    -- =======================
    -- STEP 0: OWNERSHIP VALIDATION
    -- =======================
    -- Check if any tables in the definition are owned by a different product
    SELECT po.ObjectName, po.ProductName
    INTO v_ConflictingTable, v_ConflictingOwner
    FROM _SchemaSmith_Tables t
    INNER JOIN SchemaSmith_ProductOwnership po
        ON CONVERT(po.ObjectName USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4)
        AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
        AND po.ObjectType = 'TABLE'
    WHERE CONVERT(po.ProductName USING utf8mb4) != CONVERT(p_ProductName USING utf8mb4)
    LIMIT 1;

    IF v_ConflictingTable IS NOT NULL THEN
        -- Use user variable for dynamic message (SIGNAL requires constant or user variable)
        SET @schemasmith_error_msg = CONCAT('Table ', v_ConflictingTable, ' is already owned by another product: ', v_ConflictingOwner);
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = @schemasmith_error_msg;
    END IF;

    -- =======================
    -- STEP 1: TABLE RENAMES
    -- =======================
    -- Rename tables where OldName is specified and old table exists
    -- Note: Using BINARY for comparisons to avoid collation issues between
    -- INFORMATION_SCHEMA (utf8mb3), SchemaSmith functions (utf8mb4_unicode_ci),
    -- and connection parameters (utf8mb4_0900_ai_ci)
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle table renames');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('RENAME TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      SchemaSmith_StripBacktickWrapping(t.OldName), '` TO `',
                      p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(t.TableName), '`')
        FROM _SchemaSmith_Tables t
        WHERE t.OldName IS NOT NULL
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
              WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.OldName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
              WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
          );
    ELSE
        -- Materialize old/new name pairs once (avoids referencing _SchemaSmith_Tables via the
        -- filter more than needed) so both the per-table messages, the folded RENAME TABLE
        -- statement, and the ProductOwnership update read from the same snapshot.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenames;
        CREATE TEMPORARY TABLE _SchemaSmith_TableRenames (
            OldTableName VARCHAR(128) NOT NULL,
            NewTableName VARCHAR(128) NOT NULL,
            PRIMARY KEY (OldTableName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_TableRenames (OldTableName, NewTableName)
        SELECT
            SchemaSmith_StripBacktickWrapping(t.OldName),
            SchemaSmith_StripBacktickWrapping(t.TableName)
        FROM _SchemaSmith_Tables t
        WHERE t.OldName IS NOT NULL
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
              WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.OldName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
              WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
          );

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle table renames');

        -- Per-table progress messages, set-based (preserves the per-table log lines).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Rename table `', OldTableName, '` to `', NewTableName, '`')
        FROM _SchemaSmith_TableRenames;

        -- Fold ALL renamed table pairs into ONE multi-target RENAME TABLE statement.
        -- HAVING COUNT(*) > 0 keeps the folded temp table empty (rather than one row holding a
        -- NULL Stmt from an empty GROUP_CONCAT) when there are no renames to perform.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenameStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_TableRenameStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_TableRenameStmts (Stmt)
        SELECT CONCAT('RENAME TABLE ',
                      GROUP_CONCAT(
                          CONCAT('`', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', OldTableName, '` TO `',
                                 p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', NewTableName, '`')
                          ORDER BY OldTableName SEPARATOR ', '))
        FROM _SchemaSmith_TableRenames
        HAVING COUNT(*) > 0;

        SET @v_tablerename_id := (SELECT MIN(RowId) FROM _SchemaSmith_TableRenameStmts);
        WHILE @v_tablerename_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_TableRenameStmts WHERE RowId = @v_tablerename_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_tablerename_id := (SELECT MIN(RowId) FROM _SchemaSmith_TableRenameStmts WHERE RowId > @v_tablerename_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenameStmts;

        -- Update ProductOwnership for the renamed tables (set-based join, one UPDATE for all pairs).
        UPDATE SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_TableRenames r
            ON po.ObjectName COLLATE utf8mb4_unicode_ci = r.OldTableName COLLATE utf8mb4_unicode_ci
        SET po.ObjectName = r.NewTableName
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'TABLE' COLLATE utf8mb4_unicode_ci;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenames;
    END IF;

    -- =======================
    -- STEP 2: COLUMN RENAMES
    -- =======================
    -- Rename columns where OldName is specified and old column exists
    -- Uses MySQL 8.0.14+ RENAME COLUMN syntax
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle column renames');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' RENAME COLUMN `', SchemaSmith_StripBacktickWrapping(c.OldName),
                      '` TO `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`')
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.OldName IS NOT NULL
          AND c.NewColumn = 0
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.OldName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
          );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle column renames');

        -- Per-column progress messages, set-based (preserves the per-column log lines and the
        -- exact "ALTER TABLE ... RENAME COLUMN ..." single-column text, even though the
        -- statement actually executed below folds multiple columns of the same table together).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Rename column: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' RENAME COLUMN `', SchemaSmith_StripBacktickWrapping(c.OldName),
                      '` TO `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`')
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.OldName IS NOT NULL
          AND c.NewColumn = 0
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.OldName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
          );

        -- Materialize: fold each table's column renames into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColRenameStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_ColRenameStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_ColRenameStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName, ' ',
                      GROUP_CONCAT(
                          CONCAT('RENAME COLUMN `', SchemaSmith_StripBacktickWrapping(c.OldName),
                                 '` TO `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`')
                          ORDER BY c.ColumnName SEPARATOR ', '))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.OldName IS NOT NULL
          AND c.NewColumn = 0
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.OldName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc
              WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
          )
        GROUP BY c.TableName;

        SET @v_colrename_id := (SELECT MIN(RowId) FROM _SchemaSmith_ColRenameStmts);
        WHILE @v_colrename_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_ColRenameStmts WHERE RowId = @v_colrename_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_colrename_id := (SELECT MIN(RowId) FROM _SchemaSmith_ColRenameStmts WHERE RowId > @v_colrename_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColRenameStmts;
    END IF;

    -- =======================
    -- STEP 3: COLUMN MODIFICATIONS (excludes generated status changes)
    -- =======================
    -- Output column modifications
    -- NOTE: Excludes columns that are changing their generated status since MySQL
    -- doesn't support that via ALTER MODIFY - those are handled in Step 3.5
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Modify columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' MODIFY COLUMN ', c.ColumnScript)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          -- Exclude columns where generated status is changing (regular->generated or generated->regular)
          AND NOT (
              -- Currently generated, wants to be regular
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              -- Currently regular, wants to be generated
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          )
          AND (
              -- Normalize whitespace adjacent to structural delimiters and the DECIMAL/NUMERIC
              -- synonym so a hand-authored type that differs only by spacing/synonym is not a
              -- false "modified". ENUM/SET take a separate branch: their parenthesized values are
              -- case-sensitive DATA, so they compare keyword-case-insensitively but
              -- value-case-sensitively (BINARY) via SchemaSmith_UpperDataType — the whitespace/
              -- synonym normalization would wrongly fold a real value-case change.
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN BINARY SchemaSmith_UpperDataType(isc.COLUMN_TYPE) != BINARY SchemaSmith_UpperDataType(c.DataType)
                   ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
                     != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              -- Default value changes (strip outer single quotes from JSON default for comparison,
              -- since GenerateTableJson wraps string/enum defaults in quotes for DDL but INFORMATION_SCHEMA stores raw values)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (isc.COLUMN_DEFAULT IS NULL OR BINARY isc.COLUMN_DEFAULT != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END))
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND isc.COLUMN_DEFAULT IS NOT NULL)
              -- Collation changes (only when JSON specifies a collation)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              -- Generated expression changes (both sides are generated, but expression differs)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              -- AUTO_INCREMENT removal/addition (live EXTRA vs declared IsAutoIncrement) — parity with identity removal
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
          );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Modify columns');

        -- Per-column progress messages, set-based (preserves the per-column "ALTER TABLE ...
        -- MODIFY COLUMN ..." single-column text, even though execution below folds multiple
        -- columns of the same table into one statement).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Modify column: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' MODIFY COLUMN ', c.ColumnScript)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          -- Exclude columns where generated status is changing (regular->generated or generated->regular)
          AND NOT (
              -- Currently generated, wants to be regular
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              -- Currently regular, wants to be generated
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          )
          AND (
              -- Normalize whitespace adjacent to structural delimiters and the DECIMAL/NUMERIC
              -- synonym so a hand-authored type that differs only by spacing/synonym is not a
              -- false "modified". ENUM/SET take a separate branch: their parenthesized values are
              -- case-sensitive DATA, so they compare keyword-case-insensitively but
              -- value-case-sensitively (BINARY) via SchemaSmith_UpperDataType — the whitespace/
              -- synonym normalization would wrongly fold a real value-case change.
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN BINARY SchemaSmith_UpperDataType(isc.COLUMN_TYPE) != BINARY SchemaSmith_UpperDataType(c.DataType)
                   ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
                     != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              -- Default value changes (strip outer single quotes from JSON default for comparison,
              -- since GenerateTableJson wraps string/enum defaults in quotes for DDL but INFORMATION_SCHEMA stores raw values)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (isc.COLUMN_DEFAULT IS NULL OR BINARY isc.COLUMN_DEFAULT != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END))
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND isc.COLUMN_DEFAULT IS NOT NULL)
              -- Collation changes (only when JSON specifies a collation)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              -- Generated expression changes (both sides are generated, but expression differs)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              -- AUTO_INCREMENT removal/addition (live EXTRA vs declared IsAutoIncrement) — parity with identity removal
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
          );

        -- Object-change audit (#243 E5): one row per column about to be modified. Same join +
        -- predicate as the fold below, evaluated before the ALTER (INFORMATION_SCHEMA still reflects
        -- the OLD definition); per-column (no GROUP BY). Same INFORMATION_SCHEMA read pattern the
        -- statement-build below uses — not the #337 set-based-UPDATE shape.
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'column', CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'modified'
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND NOT (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          )
          AND (
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN BINARY SchemaSmith_UpperDataType(isc.COLUMN_TYPE) != BINARY SchemaSmith_UpperDataType(c.DataType)
                   ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
                     != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (isc.COLUMN_DEFAULT IS NULL OR BINARY isc.COLUMN_DEFAULT != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END))
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND isc.COLUMN_DEFAULT IS NOT NULL)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
          );

        -- Materialize: fold each table's column modifications into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifyColStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_ModifyColStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_ModifyColStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName, ' ',
                      GROUP_CONCAT(CONCAT('MODIFY COLUMN ', c.ColumnScript) ORDER BY c.ColumnName SEPARATOR ', '))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND NOT (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          )
          AND (
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN BINARY SchemaSmith_UpperDataType(isc.COLUMN_TYPE) != BINARY SchemaSmith_UpperDataType(c.DataType)
                   ELSE REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
                     != REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC') END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (isc.COLUMN_DEFAULT IS NULL OR BINARY isc.COLUMN_DEFAULT != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END))
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND isc.COLUMN_DEFAULT IS NOT NULL)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
          )
        GROUP BY c.TableName;

        SET @v_modifycol_id := (SELECT MIN(RowId) FROM _SchemaSmith_ModifyColStmts);
        WHILE @v_modifycol_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_ModifyColStmts WHERE RowId = @v_modifycol_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_modifycol_id := (SELECT MIN(RowId) FROM _SchemaSmith_ModifyColStmts WHERE RowId > @v_modifycol_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifyColStmts;
    END IF;

    -- =======================
    -- STEP 3.5: GENERATED STATUS CHANGES (via DROP+ADD)
    -- =======================
    -- MySQL doesn't support changing a column's generated status via ALTER MODIFY
    -- So we must DROP the column and ADD it with the new definition
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Recreate columns (generated status changes)');
        -- Show DROP COLUMN statements
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      SchemaSmith_StripBacktickWrapping(c.TableName), '` DROP COLUMN `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`')
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          );
        -- Show ADD COLUMN statements
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      SchemaSmith_StripBacktickWrapping(c.TableName), '` ADD COLUMN ', c.ColumnScript)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Recreate columns (generated status changes)');

        -- Per-column progress messages, set-based (preserves the per-column log line).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Recreate column: ', SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          );

        -- Materialize DROP-phase then ADD-phase, folded per table. Two separate INSERTs (a
        -- MySQL TEMPORARY table cannot be referenced twice in one statement); the DROP-phase
        -- rows are inserted first so AUTO_INCREMENT RowId guarantees every column drop for a
        -- table runs before that table's re-adds (mirrors the FK-drop/index-drop two-phase
        -- ordering in SchemaSmith_ForeignKeyQuench).
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenStatusStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_GenStatusStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_GenStatusStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(c.TableName), '` ',
                      GROUP_CONCAT(CONCAT('DROP COLUMN `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`') ORDER BY c.ColumnName SEPARATOR ', '))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          )
        GROUP BY c.TableName;

        INSERT INTO _SchemaSmith_GenStatusStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(c.TableName), '` ',
                      GROUP_CONCAT(CONCAT('ADD COLUMN ', c.ColumnScript) ORDER BY c.ColumnName SEPARATOR ', '))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
            AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
        WHERE t.NewTable = 0
          AND c.NewColumn = 0
          AND (
              ((isc.GENERATION_EXPRESSION IS NOT NULL AND isc.GENERATION_EXPRESSION != '')
               AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = ''))
              OR
              ((isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
               AND (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''))
          )
        GROUP BY c.TableName;

        SET @v_genstatus_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenStatusStmts);
        WHILE @v_genstatus_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_GenStatusStmts WHERE RowId = @v_genstatus_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_genstatus_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenStatusStmts WHERE RowId > @v_genstatus_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenStatusStmts;
    END IF;

    -- =======================
    -- STEP 4: DROP UNUSED COLUMNS
    -- =======================
    -- Drop columns that exist in the database but are not in the JSON definition
    -- Must drop dependencies first (FKs, check constraints, indexes, generated columns)
    --
    -- Note: We use helper temp tables to avoid MySQL's "Can't reopen table" error
    -- (Error 1137) which occurs when a temp table is referenced multiple times
    -- in the same query (including in subqueries).

    -- Create helper table to copy defined columns (avoids referencing _SchemaSmith_Columns multiple times)
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedColumns;
    CREATE TEMPORARY TABLE _SchemaSmith_DefinedColumns (
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        OldName VARCHAR(128) NULL,
        INDEX idx_table_col (TableName, ColumnName),
        INDEX idx_table_old (TableName, OldName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    INSERT INTO _SchemaSmith_DefinedColumns (TableName, ColumnName, OldName)
    SELECT
        SchemaSmith_StripBacktickWrapping(c.TableName),
        SchemaSmith_StripBacktickWrapping(c.ColumnName),
        CASE WHEN c.OldName IS NOT NULL THEN SchemaSmith_StripBacktickWrapping(c.OldName) ELSE NULL END
    FROM _SchemaSmith_Columns c;

    -- Create helper table for columns to drop (used by all subsequent cursors)
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColumnsToDrop;
    CREATE TEMPORARY TABLE _SchemaSmith_ColumnsToDrop (
        TableName VARCHAR(128) NOT NULL,
        ColumnName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, ColumnName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Identify columns to drop: exist in DB but not in JSON definition
    INSERT INTO _SchemaSmith_ColumnsToDrop (TableName, ColumnName)
    SELECT
        SchemaSmith_StripBacktickWrapping(t.TableName) AS TableName,
        isc.COLUMN_NAME AS ColumnName
    FROM _SchemaSmith_Tables t
    INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
        ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
    LEFT JOIN _SchemaSmith_DefinedColumns dc
        ON dc.TableName = SchemaSmith_StripBacktickWrapping(t.TableName)
        AND (
            BINARY dc.ColumnName = BINARY isc.COLUMN_NAME
            OR (dc.OldName IS NOT NULL AND BINARY dc.OldName = BINARY isc.COLUMN_NAME)
        )
    WHERE t.NewTable = 0
      AND dc.ColumnName IS NULL
      AND p_DropColumnsRemovedFromProduct = 1
      AND COALESCE(t.DropColumnsRemovedFromProduct, 1) = 1;

    -- No-drop protection tier (#270): capture columns that WOULD have been dropped by absence but
    -- are suppressed. Same by-absence predicate as the _SchemaSmith_ColumnsToDrop build above, minus
    -- the env p_DropColumnsRemovedFromProduct gate (protection forces it false) but keeping the
    -- per-table opt-out. Materialize the INFORMATION_SCHEMA read first (Index-B crash-safety), then
    -- a discrete audit insert. Audit rows only, so it runs regardless of p_WhatIf.
    IF p_CaptureWouldDrop = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropColumns;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropColumns (
            TableName VARCHAR(128) NOT NULL,
            ColumnName VARCHAR(128) NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_WouldDropColumns (TableName, ColumnName)
        SELECT SchemaSmith_StripBacktickWrapping(t.TableName), isc.COLUMN_NAME
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        LEFT JOIN _SchemaSmith_DefinedColumns dc
            ON dc.TableName = SchemaSmith_StripBacktickWrapping(t.TableName)
            AND (
                BINARY dc.ColumnName = BINARY isc.COLUMN_NAME
                OR (dc.OldName IS NOT NULL AND BINARY dc.OldName = BINARY isc.COLUMN_NAME)
            )
        WHERE t.NewTable = 0
          AND dc.ColumnName IS NULL
          AND COALESCE(t.DropColumnsRemovedFromProduct, 1) = 1;

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'column', CONCAT(p_DatabaseName, '.', TableName, '.', ColumnName), 'wouldDrop'
        FROM _SchemaSmith_WouldDropColumns;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropColumns;
    END IF;

    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unused columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` DROP COLUMN `', ColumnName, '`')
        FROM _SchemaSmith_ColumnsToDrop;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unused columns');
        -- First, drop foreign keys that reference columns we're about to drop
        -- Note: Uses a helper table keyed on (TableName, ConstraintName), same shape as
        -- SchemaSmith_ForeignKeyQuench's _SchemaSmith_ModifiedFKs, with two separate INSERTs to
        -- avoid the MySQL optimizer bug where an OR in a JOIN against
        -- INFORMATION_SCHEMA.KEY_COLUMN_USAGE with multiple temp table rows produces incorrect
        -- (empty) results. The PRIMARY KEY also dedupes a self-referencing FK that shows up in
        -- both branches (an improvement over the original's per-branch-only DISTINCT, which
        -- could otherwise attempt to drop the same FK twice and error on the second attempt).
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_FKsToDrop (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Branch 1: FK source columns being dropped
        INSERT IGNORE INTO _SchemaSmith_FKsToDrop (TableName, ConstraintName)
        SELECT DISTINCT
            CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
            CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
            ON CONVERT(kcu.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(kcu.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND CONVERT(kcu.COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
            AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
            AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
            AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY';

        -- Branch 2: FK referenced (target) columns being dropped
        INSERT IGNORE INTO _SchemaSmith_FKsToDrop (TableName, ConstraintName)
        SELECT DISTINCT
            CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
            CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
            ON CONVERT(kcu.REFERENCED_TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND CONVERT(kcu.REFERENCED_COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
            AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
            AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
            AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY';

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop FK for column: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` DROP FOREIGN KEY `', ConstraintName, '`')
        FROM _SchemaSmith_FKsToDrop;

        -- Materialize: fold each table's FK drops into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKDropForColStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_FKDropForColStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_FKDropForColStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', ConstraintName, '`') ORDER BY ConstraintName SEPARATOR ', '))
        FROM _SchemaSmith_FKsToDrop
        GROUP BY TableName;

        SET @v_fkcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKDropForColStmts);
        WHILE @v_fkcol_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_FKDropForColStmts WHERE RowId = @v_fkcol_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_fkcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_FKDropForColStmts WHERE RowId > @v_fkcol_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKDropForColStmts;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKsToDrop;

        -- Drop check constraints that reference columns being dropped
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CKsToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_CKsToDrop (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT IGNORE INTO _SchemaSmith_CKsToDrop (TableName, ConstraintName)
        SELECT DISTINCT
            CONVERT(tc.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
            CONVERT(cc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
            ON CONVERT(cc.CONSTRAINT_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            -- Check if constraint references this column (explicit COLLATE to avoid collation mismatch)
            AND CONVERT(cc.CHECK_CLAUSE USING utf8mb4) COLLATE utf8mb4_unicode_ci
                LIKE CONCAT('%`', ctd.ColumnName COLLATE utf8mb4_unicode_ci, '`%')
        INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            ON CONVERT(tc.CONSTRAINT_SCHEMA USING utf8mb4) = CONVERT(cc.CONSTRAINT_SCHEMA USING utf8mb4)
            AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(cc.CONSTRAINT_NAME USING utf8mb4)
            AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND tc.CONSTRAINT_TYPE = 'CHECK';

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop check constraint for column: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` DROP CONSTRAINT `', ConstraintName, '`')
        FROM _SchemaSmith_CKsToDrop;

        -- Materialize: fold each table's check-constraint drops into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CKDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CKDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_CKDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP CONSTRAINT `', ConstraintName, '`') ORDER BY ConstraintName SEPARATOR ', '))
        FROM _SchemaSmith_CKsToDrop
        GROUP BY TableName;

        SET @v_ckcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_CKDropStmts);
        WHILE @v_ckcol_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_CKDropStmts WHERE RowId = @v_ckcol_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_ckcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_CKDropStmts WHERE RowId > @v_ckcol_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CKDropStmts;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CKsToDrop;

        -- Drop indexes that use columns being dropped
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxsToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_IdxsToDrop (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT IGNORE INTO _SchemaSmith_IdxsToDrop (TableName, IndexName)
        SELECT DISTINCT
            CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
            CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.STATISTICS s
            ON CONVERT(s.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(s.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND CONVERT(s.COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
        WHERE UPPER(s.INDEX_NAME) != 'PRIMARY';

        -- Message text preserves the original standalone "DROP INDEX ... ON ..." wording even
        -- though the statement actually executed below uses the equivalent
        -- "ALTER TABLE ... DROP INDEX ..." form so multiple indexes on the same table can fold
        -- into one statement.
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop index for column: DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_IdxsToDrop;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_IdxDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IdxDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP INDEX `', IndexName, '`') ORDER BY IndexName SEPARATOR ', '))
        FROM _SchemaSmith_IdxsToDrop
        GROUP BY TableName;

        SET @v_idxcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_IdxDropStmts);
        WHILE @v_idxcol_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_IdxDropStmts WHERE RowId = @v_idxcol_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_idxcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_IdxDropStmts WHERE RowId > @v_idxcol_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxDropStmts;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxsToDrop;

        -- Drop generated columns that reference columns being dropped
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColsToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_GenColsToDrop (
            TableName VARCHAR(128) NOT NULL,
            ColumnName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ColumnName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- INSERT IGNORE: a generated column referencing two-or-more dropped columns produces
        -- multiple join rows for the same (TableName, ColumnName); the original loop had no
        -- DISTINCT here either, but the folded form needs the PK to hold a single row per column.
        INSERT IGNORE INTO _SchemaSmith_GenColsToDrop (TableName, ColumnName)
        SELECT
            CONVERT(isc_gen.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
            CONVERT(isc_gen.COLUMN_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc_gen
            ON CONVERT(isc_gen.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(isc_gen.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND isc_gen.GENERATION_EXPRESSION IS NOT NULL
            AND isc_gen.GENERATION_EXPRESSION != ''
            -- Explicit COLLATE to avoid collation mismatch between INFORMATION_SCHEMA and temp table
            AND CONVERT(isc_gen.GENERATION_EXPRESSION USING utf8mb4) COLLATE utf8mb4_unicode_ci
                LIKE CONCAT('%`', ctd.ColumnName COLLATE utf8mb4_unicode_ci, '`%')
        -- Only drop generated columns that are also not in the definition
        -- (use _SchemaSmith_DefinedColumns to avoid MySQL's "Can't reopen table" error)
        LEFT JOIN _SchemaSmith_DefinedColumns dc_gen
            ON dc_gen.TableName = ctd.TableName
            AND BINARY dc_gen.ColumnName = BINARY isc_gen.COLUMN_NAME
        WHERE dc_gen.ColumnName IS NULL;

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop generated column: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` DROP COLUMN `', ColumnName, '`')
        FROM _SchemaSmith_GenColsToDrop;

        -- Materialize: fold each table's generated-column drops into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_GenColDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_GenColDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      GROUP_CONCAT(CONCAT('DROP COLUMN `', ColumnName, '`') ORDER BY ColumnName SEPARATOR ', '))
        FROM _SchemaSmith_GenColsToDrop
        GROUP BY TableName;

        SET @v_gencol_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenColDropStmts);
        WHILE @v_gencol_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_GenColDropStmts WHERE RowId = @v_gencol_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_gencol_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenColDropStmts WHERE RowId > @v_gencol_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDropStmts;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColsToDrop;

        -- Now drop the columns themselves
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop column: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      CONVERT(ctd.TableName USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                      '` DROP COLUMN `', CONVERT(ctd.ColumnName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`')
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON CONVERT(isc.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(isc.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND CONVERT(isc.COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
        -- Not a generated column (those were handled above)
        WHERE (isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '');

        -- Materialize: fold each table's remaining column drops into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_ColDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_ColDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                      CONVERT(ctd.TableName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '` ',
                      GROUP_CONCAT(CONCAT('DROP COLUMN `', CONVERT(ctd.ColumnName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`') ORDER BY ctd.ColumnName SEPARATOR ', '))
        FROM _SchemaSmith_ColumnsToDrop ctd
        INNER JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON CONVERT(isc.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(isc.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
            AND CONVERT(isc.COLUMN_NAME USING utf8mb4) = CONVERT(ctd.ColumnName USING utf8mb4)
        WHERE (isc.GENERATION_EXPRESSION IS NULL OR isc.GENERATION_EXPRESSION = '')
        GROUP BY ctd.TableName;

        SET @v_dropcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_ColDropStmts);
        WHILE @v_dropcol_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_ColDropStmts WHERE RowId = @v_dropcol_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_dropcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_ColDropStmts WHERE RowId > @v_dropcol_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColDropStmts;
    END IF;

    -- Clean up helper tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColumnsToDrop;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedColumns;

    -- =======================
    -- STEP 5: ALTER TABLE ENGINE
    -- =======================
    -- Alter table engine if different
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table engine');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ENGINE = ', t.Engine)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.Engine IS NOT NULL
          AND UPPER(ist.ENGINE) != UPPER(t.Engine);
    ELSE
        BEGIN
            DECLARE v_EngineDone INT DEFAULT FALSE;
            DECLARE v_EngineSql TEXT;
            DECLARE cur_EngineChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ENGINE = ', t.Engine) AS AlterEngineStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.Engine IS NOT NULL
                  AND UPPER(ist.ENGINE) != UPPER(t.Engine);

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_EngineDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table engine');
            SET v_EngineDone = FALSE;
            OPEN cur_EngineChanges;

            engine_changes_loop: LOOP
                FETCH cur_EngineChanges INTO v_EngineSql;
                IF v_EngineDone THEN
                    LEAVE engine_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Change engine: ', v_EngineSql));
                SET @exec_sql = v_EngineSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_EngineChanges;
        END;
    END IF;

    -- Alter table collation if different
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table collation');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
                      ' CONVERT TO CHARACTER SET ',
                      SUBSTRING_INDEX(t.Collation, '_', 1),
                      ' COLLATE ', t.Collation)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.Collation IS NOT NULL
          AND ist.TABLE_COLLATION != t.Collation;
    ELSE
        BEGIN
            DECLARE v_CollationDone INT DEFAULT FALSE;
            DECLARE v_CollationSql TEXT;
            DECLARE cur_CollationChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
                              ' CONVERT TO CHARACTER SET ',
                              SUBSTRING_INDEX(t.Collation, '_', 1),
                              ' COLLATE ', t.Collation) AS AlterCollationStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.Collation IS NOT NULL
                  AND ist.TABLE_COLLATION != t.Collation;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_CollationDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table collation');
            SET v_CollationDone = FALSE;
            OPEN cur_CollationChanges;

            collation_changes_loop: LOOP
                FETCH cur_CollationChanges INTO v_CollationSql;
                IF v_CollationDone THEN
                    LEAVE collation_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Change collation: ', v_CollationSql));
                SET @exec_sql = v_CollationSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_CollationChanges;
        END;
    END IF;

    -- =======================
    -- STEP 6: ALTER TABLE ROW_FORMAT
    -- =======================
    -- Alter table row format if different
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table row format');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ROW_FORMAT=', t.RowFormat)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.RowFormat IS NOT NULL
          AND t.RowFormat != ''
          AND UPPER(ist.ROW_FORMAT) != UPPER(t.RowFormat);
    ELSE
        BEGIN
            DECLARE v_RowFormatDone INT DEFAULT FALSE;
            DECLARE v_RowFormatSql TEXT;
            DECLARE cur_RowFormatChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ROW_FORMAT=', t.RowFormat) AS AlterRowFormatStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.RowFormat IS NOT NULL
                  AND t.RowFormat != ''
                  AND UPPER(ist.ROW_FORMAT) != UPPER(t.RowFormat);

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_RowFormatDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table row format');
            SET v_RowFormatDone = FALSE;
            OPEN cur_RowFormatChanges;

            rowformat_changes_loop: LOOP
                FETCH cur_RowFormatChanges INTO v_RowFormatSql;
                IF v_RowFormatDone THEN
                    LEAVE rowformat_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Change row format: ', v_RowFormatSql));
                SET @exec_sql = v_RowFormatSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_RowFormatChanges;
        END;
    END IF;

    -- =======================
    -- STEP 7: ALTER TABLE AUTO_INCREMENT
    -- =======================
    -- Set auto-increment seed when declared value is higher than the live value (set-if-higher, idempotent).
    -- COALESCE(ist.AUTO_INCREMENT, 0): MySQL returns NULL for AUTO_INCREMENT on an empty InnoDB table,
    -- treating NULL as 0 ensures a declared seed is applied even on tables that have never had rows.
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Set auto-increment seed');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' AUTO_INCREMENT=', t.AutoIncrementValue)
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND t.AutoIncrementValue IS NOT NULL
          AND t.AutoIncrementValue > COALESCE(ist.AUTO_INCREMENT, 0);
    ELSE
        BEGIN
            DECLARE v_AutoIncDone INT DEFAULT FALSE;
            DECLARE v_AutoIncSql TEXT;
            DECLARE cur_AutoIncChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' AUTO_INCREMENT=', t.AutoIncrementValue) AS AlterAutoIncStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND t.AutoIncrementValue IS NOT NULL
                  AND t.AutoIncrementValue > COALESCE(ist.AUTO_INCREMENT, 0);

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_AutoIncDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Set auto-increment seed');
            SET v_AutoIncDone = FALSE;
            OPEN cur_AutoIncChanges;

            autoinc_changes_loop: LOOP
                FETCH cur_AutoIncChanges INTO v_AutoIncSql;
                IF v_AutoIncDone THEN
                    LEAVE autoinc_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Set auto-increment: ', v_AutoIncSql));
                SET @exec_sql = v_AutoIncSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_AutoIncChanges;
        END;
    END IF;

    -- Update ProductOwnership for managed tables (non-WhatIf mode only)
    IF p_WhatIf = 0 THEN
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName, PreventDrop)
        SELECT p_ProductName, '', p_DatabaseName, 'TABLE', SchemaSmith_StripBacktickWrapping(t.TableName), COALESCE(t.PreventDrop, 0)
        FROM _SchemaSmith_Tables t
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
            WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        );

        -- INSERT IGNORE skips existing ownership rows, so a toggled PreventDrop would not take
        -- effect without this refresh UPDATE carrying the current per-table flag onto the row.
        UPDATE SchemaSmith_ProductOwnership po
          JOIN _SchemaSmith_Tables t
            ON CONVERT(po.ObjectName USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4)
         SET po.PreventDrop = COALESCE(t.PreventDrop, 0)
         WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
           AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
           AND po.ObjectType = 'TABLE';
    END IF;

    -- =======================
    -- No-drop protection tier (#270): when protected mode is active the caller forces
    -- p_DropTablesRemovedFromProduct to 0 so STEP 8 is skipped. Record the tables that WOULD have
    -- been dropped by absence (owned, present in the catalog, absent from the package, not sticky
    -- PreventDrop) to the ChangeAudit seam as 'wouldDrop' so the run can surface a manifest. Same
    -- by-absence predicate as STEP 8; materialized first (INFORMATION_SCHEMA read out of the DML,
    -- Index-B crash-safety), then a discrete audit insert. Audit rows only, so it runs regardless
    -- of p_WhatIf.
    IF p_CaptureWouldDrop = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Capture tables suppressed by PreventDrop (would drop by absence)');
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropTables;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropTables (
            TableName VARCHAR(128) NOT NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_WouldDropTables (TableName)
        SELECT po.ObjectName
        FROM SchemaSmith_ProductOwnership po
        WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
          AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
          AND po.ObjectType = 'TABLE'
          AND COALESCE(po.PreventDrop, 0) = 0
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
              WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_Tables t
              WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
          );

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'table', CONCAT(p_DatabaseName, '.', TableName), 'wouldDrop'
        FROM _SchemaSmith_WouldDropTables;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropTables;
    END IF;

    -- =======================
    -- STEP 8: DROP TABLES REMOVED FROM PRODUCT
    -- =======================
    -- Drop tables that are owned by this product but no longer in the definition
    IF p_DropTablesRemovedFromProduct = 1 THEN
        -- #289: drop any foreign key that REFERENCES a table about to be removed BEFORE the table
        -- drop below, otherwise the DROP TABLE fails on a still-present inbound dependency.
        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop inbound foreign keys referencing tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT DISTINCT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                                   CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                   '` DROP FOREIGN KEY `', CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`')
            FROM SchemaSmith_ProductOwnership po
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON CONVERT(kcu.REFERENCED_TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
               AND CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
            INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
               AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
               AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
               AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND COALESCE(po.PreventDrop, 0) = 0
              AND EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                  WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );
        ELSE
            -- Helper temp table mirrors the column-drop FK pattern above (avoids the MySQL
            -- optimizer bug with INFORMATION_SCHEMA.KEY_COLUMN_USAGE joins), keyed on
            -- (TableName, ConstraintName) like SchemaSmith_ForeignKeyQuench's approach.
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_InboundFKsToDrop;
            CREATE TEMPORARY TABLE _SchemaSmith_InboundFKsToDrop (
                TableName VARCHAR(128) NOT NULL,
                ConstraintName VARCHAR(128) NOT NULL,
                PRIMARY KEY (TableName, ConstraintName)
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            INSERT IGNORE INTO _SchemaSmith_InboundFKsToDrop (TableName, ConstraintName)
            SELECT DISTINCT
                CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
            FROM SchemaSmith_ProductOwnership po
            INNER JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                ON CONVERT(kcu.REFERENCED_TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
               AND CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
            INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
               AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
               AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
               AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND COALESCE(po.PreventDrop, 0) = 0
              AND EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                  WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop inbound foreign keys referencing tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Drop inbound FK: ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                          '` DROP FOREIGN KEY `', ConstraintName, '`')
            FROM _SchemaSmith_InboundFKsToDrop;

            -- Materialize: fold each table's inbound FK drops into one multi-clause ALTER, then drain.
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_InboundFKDropStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_InboundFKDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_InboundFKDropStmts (Stmt)
            SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                          GROUP_CONCAT(CONCAT('DROP FOREIGN KEY `', ConstraintName, '`') ORDER BY ConstraintName SEPARATOR ', '))
            FROM _SchemaSmith_InboundFKsToDrop
            GROUP BY TableName;

            SET @v_inboundfk_id := (SELECT MIN(RowId) FROM _SchemaSmith_InboundFKDropStmts);
            WHILE @v_inboundfk_id IS NOT NULL DO
                SELECT Stmt INTO @exec_sql FROM _SchemaSmith_InboundFKDropStmts WHERE RowId = @v_inboundfk_id;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                SET @v_inboundfk_id := (SELECT MIN(RowId) FROM _SchemaSmith_InboundFKDropStmts WHERE RowId > @v_inboundfk_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_InboundFKDropStmts;

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_InboundFKsToDrop;
        END IF;

        -- A CustomTableDrop hook (e.g. a recyclebin pattern) replaces the plain DROP TABLE when the
        -- user has installed a SchemaSmith_CustomTableDrop procedure in this database, mirroring the
        -- SQL Server / PostgreSQL hook.
        SET @has_custom_drop = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES
                                WHERE CONVERT(ROUTINE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                                  AND ROUTINE_NAME = 'SchemaSmith_CustomTableDrop'
                                  AND ROUTINE_TYPE = 'PROCEDURE');

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(),
                   CASE WHEN @has_custom_drop = 1
                        THEN CONCAT('CALL SchemaSmith_CustomTableDrop(''', p_DatabaseName COLLATE utf8mb4_unicode_ci, ''', ''', po.ObjectName, ''')')
                        ELSE CONCAT('DROP TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', po.ObjectName, '`')
                        END
            FROM SchemaSmith_ProductOwnership po
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND COALESCE(po.PreventDrop, 0) = 0
              -- Table exists
              AND EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                  WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              -- Not in current definition
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );
        ELSE
            -- Materialize tables to drop: table name + the exact per-table drop statement
            -- (CALL SchemaSmith_CustomTableDrop(...) when the hook is installed, else DROP TABLE).
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TablesToDrop;
            CREATE TEMPORARY TABLE _SchemaSmith_TablesToDrop (
                RowId INT AUTO_INCREMENT PRIMARY KEY,
                TableName VARCHAR(128) NOT NULL,
                DropSql TEXT NOT NULL
            ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

            INSERT INTO _SchemaSmith_TablesToDrop (TableName, DropSql)
            SELECT
                po.ObjectName,
                CASE WHEN @has_custom_drop = 1
                     THEN CONCAT('CALL SchemaSmith_CustomTableDrop(''', p_DatabaseName COLLATE utf8mb4_unicode_ci, ''', ''', po.ObjectName, ''')')
                     ELSE CONCAT('DROP TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', po.ObjectName, '`')
                     END
            FROM SchemaSmith_ProductOwnership po
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND COALESCE(po.PreventDrop, 0) = 0
              -- Table exists
              AND EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                  WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                    AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              -- Not in current definition
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Drop table: ', TableName)
            FROM _SchemaSmith_TablesToDrop;

            -- Object-change audit (#243 E5): one row per table about to be dropped (set-based over the
            -- computed _SchemaSmith_TablesToDrop temp; the drop below folds them into one statement).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'table', TableName, 'dropped'
            FROM _SchemaSmith_TablesToDrop;

            IF @has_custom_drop = 1 THEN
                -- The CustomTableDrop hook takes exactly one table per CALL, so multiple tables
                -- can't fold into a single statement here. Drain via WHILE (still removes the
                -- cursor, even though each row stays its own statement).
                SET @v_droptbl_id := (SELECT MIN(RowId) FROM _SchemaSmith_TablesToDrop);
                WHILE @v_droptbl_id IS NOT NULL DO
                    SELECT DropSql INTO @exec_sql FROM _SchemaSmith_TablesToDrop WHERE RowId = @v_droptbl_id;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                    SET @v_droptbl_id := (SELECT MIN(RowId) FROM _SchemaSmith_TablesToDrop WHERE RowId > @v_droptbl_id);
                END WHILE;
            ELSE
                -- Fold ALL tables to drop into ONE multi-target DROP TABLE statement.
                DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropTableFoldStmt;
                CREATE TEMPORARY TABLE _SchemaSmith_DropTableFoldStmt (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
                    ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
                INSERT INTO _SchemaSmith_DropTableFoldStmt (Stmt)
                SELECT CONCAT('DROP TABLE ',
                              GROUP_CONCAT(CONCAT('`', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`') ORDER BY TableName SEPARATOR ', '))
                FROM _SchemaSmith_TablesToDrop
                HAVING COUNT(*) > 0;

                SET @v_droptblfold_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropTableFoldStmt);
                WHILE @v_droptblfold_id IS NOT NULL DO
                    SELECT Stmt INTO @exec_sql FROM _SchemaSmith_DropTableFoldStmt WHERE RowId = @v_droptblfold_id;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                    SET @v_droptblfold_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropTableFoldStmt WHERE RowId > @v_droptblfold_id);
                END WHILE;
                DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropTableFoldStmt;
            END IF;

            -- Remove dropped tables (and their indexes) from ProductOwnership, set-based.
            DELETE po FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_TablesToDrop d
                ON CONVERT(po.ObjectName USING utf8mb4) = CONVERT(d.TableName USING utf8mb4)
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE';

            DELETE po FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_TablesToDrop d
                ON CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4) = CONVERT(d.TableName USING utf8mb4)
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'INDEX';

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TablesToDrop;
        END IF;

        -- #270: report tables removed from the product but retained because PreventDrop is set.
        -- The protected table still exists and is still absent from _SchemaSmith_Tables in both the
        -- WhatIf and live paths (it was excluded from the drop candidate sets above), so this mirror
        -- set is correct either way. Discrete INFORMATION_SCHEMA read (Index-B crash-safety).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('Table ', po.ObjectName, ' removed from product but PreventDrop is set — skipping drop (protected)')
        FROM SchemaSmith_ProductOwnership po
        WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
          AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
          AND po.ObjectType = 'TABLE'
          AND COALESCE(po.PreventDrop, 0) = 1
          AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                       WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                         AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4))
          AND NOT EXISTS (SELECT 1 FROM _SchemaSmith_Tables t
                            WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4));
    END IF;

    -- =======================
    -- STEP 9 (D5): CATALOG-RECONCILE PRUNE
    -- =======================
    -- Parity prune for tables dropped OUT of band (a migration or DBA dropped the physical table
    -- without going through SchemaSmith), so their ownership rows don't go stale. The STEP 8 cleanup
    -- only removes ownership for tables THIS run dropped, so protected tables keep ownership (intended).
    -- Live path only (WhatIf must not mutate ownership). Materialize the catalog read into a temp
    -- table first (a SELECT, not DML), then DELETE against the temp — matching the STEP 8 ELSE
    -- crash-safety convention so no INFORMATION_SCHEMA read runs inside DML (Index-B).
    IF p_WhatIf = 0 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_OrphanedOwnership;
        CREATE TEMPORARY TABLE _SchemaSmith_OrphanedOwnership (Id INT PRIMARY KEY)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_OrphanedOwnership (Id)
        SELECT po.Id
          FROM SchemaSmith_ProductOwnership po
          WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
            AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND po.ObjectType = 'TABLE'
            AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                             WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                               AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4));
        DELETE po FROM SchemaSmith_ProductOwnership po
          JOIN _SchemaSmith_OrphanedOwnership o ON o.Id = po.Id;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_OrphanedOwnership;
    END IF;

END//

DELIMITER ;
