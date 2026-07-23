-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_MissingTableAndColumnQuench//

CREATE PROCEDURE SchemaSmith_MissingTableAndColumnQuench(
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure creates missing tables and adds missing columns.
    -- It reads from the _SchemaSmith_Tables and _SchemaSmith_Columns temp tables
    -- which are populated by the JSON parsing in SchemaSmith_ParseTableJson.
    --
    -- Column ordering:
    --   - Non-generated columns are ordered by OrdinalPosition
    --   - Generated columns are added after non-generated columns, ordered by
    --     DependencyLevel (to handle dependencies between generated columns)
    --     then by OrdinalPosition

    DECLARE v_Done INT DEFAULT FALSE;
    DECLARE v_Sql TEXT;
    DECLARE v_StatusTableName VARCHAR(128);
    DECLARE v_StatusVariant VARCHAR(128);

    -- Cursor for CREATE TABLE statements (non-generated columns only, ordered by OrdinalPosition).
    -- Still cursor-driven in create_tables_loop below: each new table is a distinct standalone
    -- CREATE TABLE statement (not an ALTER clause), so there is nothing to fold into a single
    -- multi-clause statement, and MySQL PREPARE only accepts one statement at a time.
    DECLARE cur_NewTables CURSOR FOR
        SELECT
            t.TableName,
            t.VariantName,
            CONCAT(
                'CREATE TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' (',
                GROUP_CONCAT(c.ColumnScript ORDER BY c.OrdinalPosition SEPARATOR ', '),
                COALESCE(t.AutoIncrementKeyClause, ''),
                COALESCE(
                    (SELECT CONCAT(', PRIMARY KEY (', i.IndexColumns, ')')
                     FROM _SchemaSmith_Indexes i
                     WHERE i.TableName = t.TableName AND i.IsPrimaryKey = 1),
                    ''
                ),
                ') ENGINE=', COALESCE(t.Engine, 'InnoDB'),
                CASE WHEN t.RowFormat IS NOT NULL AND t.RowFormat != ''
                     THEN CONCAT(' ROW_FORMAT=', t.RowFormat)
                     ELSE '' END,
                CASE WHEN t.AutoIncrementValue IS NOT NULL
                     THEN CONCAT(' AUTO_INCREMENT=', t.AutoIncrementValue)
                     ELSE '' END
            ) AS CreateTableStatement
        FROM _SchemaSmith_Tables t
        INNER JOIN _SchemaSmith_Columns c ON c.TableName = t.TableName
        WHERE t.NewTable = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
        GROUP BY t.TableName, t.VariantName, t.Engine, t.RowFormat, t.AutoIncrementValue;

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_Done = TRUE;

    SET SESSION group_concat_max_len = 1000000;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN MissingTableAndColumnQuench');

    -- A CustomTableRestore hook restores tables being added in case they were custom-dropped
    -- (recycled) previously; mirrors the SQL Server / PostgreSQL hook.
    SET @has_custom_restore = (SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES
                               WHERE CONVERT(ROUTINE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                                 AND ROUTINE_NAME = 'SchemaSmith_CustomTableRestore'
                                 AND ROUTINE_TYPE = 'PROCEDURE');

    IF p_WhatIf = 1 THEN
        -- WhatIf mode: output the actual SQL that would be executed

        IF @has_custom_restore = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Attempt custom table restore for tables being added');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('CALL SchemaSmith_CustomTableRestore(''', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, ''', ''', SchemaSmith_StripBacktickWrapping(t.TableName), ''')')
            FROM _SchemaSmith_Tables t
            WHERE t.NewTable = 1;
        END IF;

        -- Step 1: Show CREATE TABLE statements (set-based; same CONCAT shape as cur_NewTables).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing tables');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT(
                      'CREATE TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' (',
                      GROUP_CONCAT(c.ColumnScript ORDER BY c.OrdinalPosition SEPARATOR ', '),
                      COALESCE(t.AutoIncrementKeyClause, ''),
                      COALESCE(
                          (SELECT CONCAT(', PRIMARY KEY (', i.IndexColumns, ')')
                           FROM _SchemaSmith_Indexes i
                           WHERE i.TableName = t.TableName AND i.IsPrimaryKey = 1),
                          ''
                      ),
                      ') ENGINE=', COALESCE(t.Engine, 'InnoDB'),
                      CASE WHEN t.RowFormat IS NOT NULL AND t.RowFormat != ''
                           THEN CONCAT(' ROW_FORMAT=', t.RowFormat)
                           ELSE '' END,
                      CASE WHEN t.AutoIncrementValue IS NOT NULL
                           THEN CONCAT(' AUTO_INCREMENT=', t.AutoIncrementValue)
                           ELSE '' END)
        FROM _SchemaSmith_Tables t
        INNER JOIN _SchemaSmith_Columns c ON c.TableName = t.TableName
        WHERE t.NewTable = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
        GROUP BY t.TableName, t.VariantName, t.Engine, t.RowFormat, t.AutoIncrementValue;

        -- Step 2: Show ALTER TABLE ADD COLUMN for new columns on existing tables (set-based;
        -- one row per column, matching the per-column statement the ELSE branch would issue
        -- one-at-a-time before it folds them into a single ALTER per table).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing columns to existing tables');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' ADD COLUMN ', c.ColumnScript)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE t.NewTable = 0
          AND c.NewColumn = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
        ORDER BY c.TableName, c.OrdinalPosition;

        -- #363: WhatIf twins of the ELSE-branch 'table'/'created' (cursor loop) and 'column'/'created'
        -- audits. Set-based over the same temp-table sources; the new-table's own columns are covered
        -- by the table row (NewTable = 0 only for the column twin), matching the real audits.
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'table', t.TableName, 'wouldCreate'
        FROM _SchemaSmith_Tables t
        WHERE t.NewTable = 1;

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'column', CONCAT(c.TableName, '.', c.ColumnName), 'wouldCreate'
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE t.NewTable = 0
          AND c.NewColumn = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '');

    ELSE
        -- CustomTableRestore hook: attempt to restore tables being added in case they were
        -- custom-dropped (recycled) previously, then mark any that now exist as not-new so the
        -- create step below does not recreate them empty (preserving restored data).
        IF @has_custom_restore = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Attempt custom table restore for tables being added');

            -- Each restore is a standalone CALL statement, not an ALTER clause that can fold into
            -- another table's statement, so materialize one CALL per table and drain via WHILE
            -- instead of the per-row cursor this replaces.
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_RestoreStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_RestoreStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_RestoreStmts (Stmt)
            SELECT CONCAT('CALL SchemaSmith_CustomTableRestore(''', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, ''', ''', SchemaSmith_StripBacktickWrapping(t.TableName), ''')')
            FROM _SchemaSmith_Tables t
            WHERE t.NewTable = 1;

            SET @v_restore_id := (SELECT MIN(RowId) FROM _SchemaSmith_RestoreStmts);
            WHILE @v_restore_id IS NOT NULL DO
                SELECT Stmt INTO @exec_sql FROM _SchemaSmith_RestoreStmts WHERE RowId = @v_restore_id;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                SET @v_restore_id := (SELECT MIN(RowId) FROM _SchemaSmith_RestoreStmts WHERE RowId > @v_restore_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_RestoreStmts;

            UPDATE _SchemaSmith_Tables t
            SET t.NewTable = 0
            WHERE t.NewTable = 1
              AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist
                          WHERE CONVERT(ist.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                            AND CONVERT(ist.TABLE_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4));

            -- NewColumn was set at parse time, before the restore brought the table back, so the
            -- restored table's columns are still flagged as new. Clear the flag for any column that
            -- now exists so the add-columns step does not try to re-add it (duplicate column error).
            UPDATE _SchemaSmith_Columns c
            SET c.NewColumn = 0
            WHERE c.NewColumn = 1
              AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS ic
                          WHERE CONVERT(ic.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                            AND CONVERT(ic.TABLE_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(c.TableName) USING utf8mb4)
                            AND CONVERT(ic.COLUMN_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(c.ColumnName) USING utf8mb4));
        END IF;

        -- Step 1: Create new tables (with non-generated columns only)
        -- INTRINSIC — left as a cursor. Each new table is a distinct standalone CREATE TABLE
        -- statement (not an ALTER clause), so there is nothing to fold into a single multi-clause
        -- statement, and MySQL PREPARE only accepts one statement at a time.
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing tables');
        SET v_Done = FALSE;
        OPEN cur_NewTables;

        create_tables_loop: LOOP
            FETCH cur_NewTables INTO v_StatusTableName, v_StatusVariant, v_Sql;
            IF v_Done THEN
                LEAVE create_tables_loop;
            END IF;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create table ', v_StatusTableName,
                CASE WHEN COALESCE(v_StatusVariant, '') <> '' THEN CONCAT(' (variant: ', v_StatusVariant, ')') ELSE '' END));
            SET @exec_sql = v_Sql;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'table', v_StatusTableName, 'created');
            DEALLOCATE PREPARE stmt;
        END LOOP;

        CLOSE cur_NewTables;

        -- Step 2: Add non-generated columns to existing tables
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing columns to existing tables');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Add column: ',
                      CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                             ' ADD COLUMN ', c.ColumnScript),
                      CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE t.NewTable = 0
          AND c.NewColumn = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
        ORDER BY c.TableName, c.OrdinalPosition;

        -- Fold each table's missing-column adds into one multi-clause ALTER, materialize, execute.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_AddColumnStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_AddColumnStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_AddColumnStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName, ' ',
                      GROUP_CONCAT(CONCAT('ADD COLUMN ', c.ColumnScript) ORDER BY c.OrdinalPosition SEPARATOR ', '))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE t.NewTable = 0
          AND c.NewColumn = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
        GROUP BY c.TableName;

        SET @v_addcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_AddColumnStmts);
        WHILE @v_addcol_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_AddColumnStmts WHERE RowId = @v_addcol_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_addcol_id := (SELECT MIN(RowId) FROM _SchemaSmith_AddColumnStmts WHERE RowId > @v_addcol_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_AddColumnStmts;

        -- Object-change audit (#243 E5): one row per physical column added to an existing table.
        -- Set-based over temp tables only (no INFORMATION_SCHEMA — not the #337 segfault shape).
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'column', CONCAT(c.TableName, '.', c.ColumnName), 'created'
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE t.NewTable = 0
          AND c.NewColumn = 1
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '');

    END IF;

END//

DELIMITER ;
