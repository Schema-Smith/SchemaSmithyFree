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
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
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

    -- =========================================================================
    -- Degrade column DEFAULT expressions below MySQL 8.0.13 (extraction already recognises this stored
    -- form -- COLUMN_DEFAULT LIKE '(%' -- see GenerateTableJson). MariaDB has supported expression
    -- defaults since 10.2.1 (MDEV-10134), at/below our 10.2 floor, so SchemaSmith_SupportsDefaultExpression()
    -- never gates MariaDB -- this branch is MySQL-only. Below the threshold DEFAULT (<expr>) is a hard
    -- syntax error (unlike a parse-and-ignored clause), so there is no safe partial emit: 'fail' aborts
    -- naming the offending column(s); 'warn' (default) skips the column entirely -- the CREATE TABLE and
    -- ADD COLUMN emit sites below exclude it via the identical predicate -- and records a 'downgraded'
    -- manifest row per column so the run stays idempotent and visible.
    -- =========================================================================
    IF SchemaSmith_SupportsDefaultExpression() = 0
       AND EXISTS (SELECT 1 FROM _SchemaSmith_Columns c
                   INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                   WHERE (t.NewTable = 1 OR c.NewColumn = 1)
                     AND c.IsAutoIncrement = 0
                     AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
                     AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%') THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail' THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Column DEFAULT expression unsupported (requires MySQL 8.0.13): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE (t.NewTable = 1 OR c.NewColumn = 1)
              AND c.IsAutoIncrement = 0
              AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
              AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%';
            SET @ss_msg = CONCAT('Column DEFAULT expressions require MySQL 8.0.13 (detected ',
                                 SchemaSmith_ServerVersionNum(), '); see the deploy log for the unsupported column(s).');
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Skipping column (DEFAULT expression requires MySQL 8.0.13 - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE (t.NewTable = 1 OR c.NewColumn = 1)
              AND c.IsAutoIncrement = 0
              AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
              AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%';
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'column (DEFAULT expression, MySQL 8.0.13)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'downgraded'
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE (t.NewTable = 1 OR c.NewColumn = 1)
              AND c.IsAutoIncrement = 0
              AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
              AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%';
        END IF;
    END IF;

    IF p_WhatIf = 1 THEN
        -- WhatIf mode: output the actual SQL that would be executed

        -- Declarative renames would run here (before add-columns). Log the statements only.
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('RENAME TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`',
                      SchemaSmith_StripBacktickWrapping(t.OldName), '` TO `',
                      CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(t.TableName), '`')
        FROM _SchemaSmith_Tables t
        WHERE t.OldName IS NOT NULL
          AND t.NewTable = 0
          AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.OldName))
          AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES ist WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName));

        -- Descriptive (version-agnostic) preview: the executed DDL is RENAME COLUMN or, below MySQL 8.0 /
        -- MariaDB 10.5.2, CHANGE COLUMN preserving the current definition (see the real-path block below).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Rename column `', SchemaSmith_StripBacktickWrapping(c.OldName),
                      '` to `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '` on `', c.TableName, '`')
        FROM _SchemaSmith_Columns c
        WHERE c.OldName IS NOT NULL
          AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName) AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.OldName))
          AND NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc WHERE BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName) AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName));

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
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
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
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
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
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0);

    ELSE
        -- =======================
        -- DECLARATIVE RENAMES (OldName) — run BEFORE create/add-columns
        -- =======================
        -- Table + column renames execute here, ahead of add-columns, so a carried column (unchanged
        -- across the rename) or a newly-added column targets the post-rename table name — parity with
        -- SQL Server / PostgreSQL, which rename before adding columns. ProductOwnership is reconciled
        -- old->new later by ModifiedTableQuench (which has the product name). BINARY comparisons avoid
        -- collation clashes between INFORMATION_SCHEMA (utf8mb3), SchemaSmith functions
        -- (utf8mb4_unicode_ci), and connection params (utf8mb4_0900_ai_ci).

        -- Table renames: fold all old->new pairs into one multi-target RENAME TABLE, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenameStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_TableRenameStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_TableRenameStmts (Stmt)
        SELECT CONCAT('RENAME TABLE ',
                      GROUP_CONCAT(
                          CONCAT('`', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(t.OldName), '` TO `',
                                 CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(t.TableName), '`')
                          ORDER BY t.TableName SEPARATOR ', '))
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
          )
        HAVING COUNT(*) > 0;

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Rename table `', SchemaSmith_StripBacktickWrapping(t.OldName), '` to `', SchemaSmith_StripBacktickWrapping(t.TableName), '`')
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

        SET @v_tablerename_id := (SELECT MIN(RowId) FROM _SchemaSmith_TableRenameStmts);
        WHILE @v_tablerename_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_TableRenameStmts WHERE RowId = @v_tablerename_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_tablerename_id := (SELECT MIN(RowId) FROM _SchemaSmith_TableRenameStmts WHERE RowId > @v_tablerename_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenameStmts;

        -- Column renames: fold each table's column renames into one multi-clause ALTER, then drain.
        -- Catalog-based predicate (old column present, new column absent) so a column rename that
        -- rides a table rename also fires — its parse-time NewColumn flag is unreliable because the
        -- new table name did not exist when the flag was computed.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColRenameStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_ColRenameStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- `ALTER TABLE ... RENAME COLUMN old TO new` needs MySQL 8.0 / MariaDB 10.5.2+. Below that
        -- (MySQL 5.7 / MariaDB 10.2-10.5) reproduce the rename with `CHANGE COLUMN old new <def>`,
        -- reconstructing the OLD column's current definition from INFORMATION_SCHEMA (type / charset /
        -- collate / nullability / generated / auto_increment / comment). DEFAULT is intentionally OMITTED:
        -- the subsequent ModifiedTableQuench pass reconciles the column to its desired default in the same
        -- deploy, which sidesteps the cross-engine COLUMN_DEFAULT-quoting divergence (MySQL 5.7 returns
        -- string defaults unquoted, MariaDB/8.0 quoted) and keeps the rename data-preserving.
        INSERT INTO _SchemaSmith_ColRenameStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName, ' ',
                      GROUP_CONCAT(
                          IF(SchemaSmith_SupportsRenameColumn() = 1,
                             CONCAT('RENAME COLUMN `', SchemaSmith_StripBacktickWrapping(c.OldName),
                                    '` TO `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`'),
                             CONCAT('CHANGE COLUMN `', SchemaSmith_StripBacktickWrapping(c.OldName),
                                    '` `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '` ',
                                    CONVERT(isc.COLUMN_TYPE USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                    IF(isc.CHARACTER_SET_NAME IS NOT NULL,
                                       CONCAT(' CHARACTER SET ', CONVERT(isc.CHARACTER_SET_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                              ' COLLATE ', CONVERT(isc.COLLATION_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci), ''),
                                    IF(CONVERT(isc.EXTRA USING utf8mb4) LIKE '%GENERATED%',
                                       CONCAT(' GENERATED ALWAYS AS (', CONVERT(isc.GENERATION_EXPRESSION USING utf8mb4) COLLATE utf8mb4_unicode_ci, ') ',
                                              IF(CONVERT(isc.EXTRA USING utf8mb4) LIKE '%STORED%', 'STORED', 'VIRTUAL')), ''),
                                    IF(CONVERT(isc.IS_NULLABLE USING utf8mb4) = 'NO', ' NOT NULL', ' NULL'),
                                    IF(CONVERT(isc.EXTRA USING utf8mb4) LIKE '%auto_increment%', ' AUTO_INCREMENT', ''),
                                    IF(COALESCE(CONVERT(isc.COLUMN_COMMENT USING utf8mb4), '') <> '',
                                       CONCAT(' COMMENT ', QUOTE(CONVERT(isc.COLUMN_COMMENT USING utf8mb4) COLLATE utf8mb4_unicode_ci)), '')))
                          ORDER BY c.ColumnName SEPARATOR ', '))
        FROM _SchemaSmith_Columns c
        JOIN INFORMATION_SCHEMA.COLUMNS isc
            ON BINARY isc.TABLE_SCHEMA = BINARY p_DatabaseName
           AND BINARY isc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
           AND BINARY isc.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.OldName)
        WHERE c.OldName IS NOT NULL
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS isc2
              WHERE BINARY isc2.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY isc2.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                AND BINARY isc2.COLUMN_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ColumnName)
          )
        GROUP BY c.TableName;

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Rename column `', SchemaSmith_StripBacktickWrapping(c.OldName),
                      '` to `', SchemaSmith_StripBacktickWrapping(c.ColumnName), '` on `', c.TableName, '`')
        FROM _SchemaSmith_Columns c
        WHERE c.OldName IS NOT NULL
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

        SET @v_colrename_id := (SELECT MIN(RowId) FROM _SchemaSmith_ColRenameStmts);
        WHILE @v_colrename_id IS NOT NULL DO
            SELECT Stmt INTO @exec_sql FROM _SchemaSmith_ColRenameStmts WHERE RowId = @v_colrename_id;
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @v_colrename_id := (SELECT MIN(RowId) FROM _SchemaSmith_ColRenameStmts WHERE RowId > @v_colrename_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ColRenameStmts;

        -- After renames, clear NewColumn for any column now present under its (possibly renamed)
        -- table: parse flagged carried/renamed columns as new because it checked under the
        -- post-rename table name, which did not exist yet. Without this, add-columns below would try
        -- to re-add an existing column (duplicate-column error). Mirrors the CustomTableRestore fixup.
        UPDATE _SchemaSmith_Columns c
        SET c.NewColumn = 0
        WHERE c.NewColumn = 1
          AND EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS ic
                      WHERE CONVERT(ic.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                        AND CONVERT(ic.TABLE_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(c.TableName) USING utf8mb4)
                        AND CONVERT(ic.COLUMN_NAME USING utf8mb4) = CONVERT(SchemaSmith_StripBacktickWrapping(c.ColumnName) USING utf8mb4));

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
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
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
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
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
          AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0);

    END IF;

END//

DELIMITER ;
