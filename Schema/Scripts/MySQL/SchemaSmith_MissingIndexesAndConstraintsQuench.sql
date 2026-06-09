-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_MissingIndexesAndConstraintsQuench//

CREATE PROCEDURE SchemaSmith_MissingIndexesAndConstraintsQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure creates, modifies, renames, and drops indexes and check constraints.
    -- It reads from the _SchemaSmith_Indexes and _SchemaSmith_CheckConstraints
    -- temp tables populated by ParseTableJson.
    -- Foreign keys are handled separately by SchemaSmith_ForeignKeyQuench.

    DECLARE v_Done INT DEFAULT FALSE;
    DECLARE v_Sql TEXT;
    DECLARE v_TableName VARCHAR(128);
    DECLARE v_IndexName VARCHAR(128);
    DECLARE v_ConstraintName VARCHAR(128);

    DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_Done = TRUE;

    SET SESSION group_concat_max_len = 1000000;

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN MissingIndexesAndConstraintsQuench');

    -- =========================================================================
    -- STEP 0: Add missing generated columns (deferred from MissingTableAndColumnQuench
    -- to ensure dependencies like functions are deployed first)
    -- =========================================================================
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing generated columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Add generated column: ', c.TableName, '.', c.ColumnName,
            CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END)
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.GeneratedExpression IS NOT NULL
          AND TRIM(c.GeneratedExpression) != ''
          AND c.NewColumn = 1
        ORDER BY c.TableName, c.DependencyLevel, c.OrdinalPosition;
    ELSE
        BEGIN
            DECLARE v_GenDone INT DEFAULT FALSE;
            DECLARE v_GenSql TEXT;
            DECLARE v_GenVariant VARCHAR(128);

            DECLARE cur_GeneratedColumns CURSOR FOR
                SELECT
                    c.VariantName,
                    CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                           ' ADD COLUMN ', c.ColumnScript) AS AlterTableStatement
                FROM _SchemaSmith_Columns c
                INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                WHERE c.GeneratedExpression IS NOT NULL
                  AND TRIM(c.GeneratedExpression) != ''
                  AND c.NewColumn = 1
                ORDER BY c.TableName, c.DependencyLevel, c.OrdinalPosition;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_GenDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing generated columns');
            SET v_GenDone = FALSE;
            OPEN cur_GeneratedColumns;

            gen_cols_loop: LOOP
                FETCH cur_GeneratedColumns INTO v_GenVariant, v_GenSql;
                IF v_GenDone THEN
                    LEAVE gen_cols_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Add generated column: ', v_GenSql,
                    CASE WHEN COALESCE(v_GenVariant, '') <> '' THEN CONCAT(' (variant: ', v_GenVariant, ')') ELSE '' END));
                SET @exec_sql = v_GenSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_GeneratedColumns;
        END;
    END IF;

    -- =========================================================================
    -- STEP 1: Detect index renames (same columns, different name)
    -- =========================================================================
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenames;
    CREATE TEMPORARY TABLE _SchemaSmith_IndexRenames (
        TableName VARCHAR(128) NOT NULL,
        OldIndexName VARCHAR(128) NOT NULL,
        NewIndexName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, OldIndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Find indexes where the definition matches but the name is different
    -- An index is considered a rename candidate if:
    -- 1. The new index name doesn't exist
    -- 2. The old index exists and is owned by the product
    -- 3. The column list matches exactly
    INSERT INTO _SchemaSmith_IndexRenames (TableName, OldIndexName, NewIndexName)
    SELECT
        SchemaSmith_StripBacktickWrapping(i.TableName) AS TableName,
        CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci AS OldIndexName,
        SchemaSmith_StripBacktickWrapping(i.IndexName) AS NewIndexName
    FROM _SchemaSmith_Indexes i
    JOIN INFORMATION_SCHEMA.STATISTICS s
        ON BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
        AND s.SEQ_IN_INDEX = 1
    JOIN SchemaSmith_ProductOwnership po
        ON BINARY po.ProductName = BINARY p_ProductName
        AND BINARY po.ObjectSchema = BINARY p_DatabaseName
        AND po.ObjectType = 'INDEX'
        AND BINARY po.ObjectName = BINARY CONCAT(CONVERT(s.TABLE_NAME USING utf8mb4), '.', CONVERT(s.INDEX_NAME USING utf8mb4))
    WHERE i.IsPrimaryKey = 0
      -- New index name doesn't exist
      AND NOT EXISTS (
          SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s2
          WHERE BINARY s2.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY s2.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
            AND BINARY s2.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
      )
      -- Old index exists with same columns (compare normalized column list)
      AND BINARY SchemaSmith_NormalizeIndexColumns(i.IndexColumns) = BINARY (
          SELECT GROUP_CONCAT(
              CONCAT('`', sc.COLUMN_NAME, '`',
                     CASE WHEN BINARY sc.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
              ORDER BY sc.SEQ_IN_INDEX
              SEPARATOR ','
          )
          FROM INFORMATION_SCHEMA.STATISTICS sc
          WHERE BINARY sc.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY sc.TABLE_NAME = BINARY s.TABLE_NAME
            AND BINARY sc.INDEX_NAME = BINARY s.INDEX_NAME
      )
      -- Same uniqueness
      AND i.IsUnique = (s.NON_UNIQUE = 0);

    -- Handle renames
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle index renames');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` RENAME INDEX `', OldIndexName, '` TO `', NewIndexName, '`')
        FROM _SchemaSmith_IndexRenames;
    ELSE
        BEGIN
            DECLARE v_RenameDone INT DEFAULT FALSE;
            DECLARE v_RenameTable VARCHAR(128);
            DECLARE v_OldName VARCHAR(128);
            DECLARE v_NewName VARCHAR(128);
            DECLARE v_RenameSql TEXT;

            DECLARE cur_Renames CURSOR FOR
                SELECT TableName, OldIndexName, NewIndexName
                FROM _SchemaSmith_IndexRenames;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_RenameDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle index renames');
            SET v_RenameDone = FALSE;
            OPEN cur_Renames;

            rename_loop: LOOP
                FETCH cur_Renames INTO v_RenameTable, v_OldName, v_NewName;
                IF v_RenameDone THEN
                    LEAVE rename_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Rename index: ', v_RenameTable, '.', v_OldName, ' -> ', v_NewName));
                SET v_RenameSql = CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', v_RenameTable,
                                         '` RENAME INDEX `', v_OldName, '` TO `', v_NewName, '`');
                SET @exec_sql = v_RenameSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;

                -- Update ProductOwnership with new name
                UPDATE SchemaSmith_ProductOwnership
                SET ObjectName = CONCAT(v_RenameTable, '.', v_NewName)
                WHERE BINARY ProductName = BINARY p_ProductName
                  AND BINARY ObjectSchema = BINARY p_DatabaseName
                  AND ObjectType = 'INDEX'
                  AND BINARY ObjectName = BINARY CONCAT(v_RenameTable, '.', v_OldName);
            END LOOP;

            CLOSE cur_Renames;
        END;
    END IF;

    -- =========================================================================
    -- STEP 2: Detect modified indexes (same name, different definition)
    -- =========================================================================
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedIndexes;
    CREATE TEMPORARY TABLE _SchemaSmith_ModifiedIndexes (
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Find indexes where the name matches but the definition is different
    INSERT INTO _SchemaSmith_ModifiedIndexes (TableName, IndexName)
    SELECT
        SchemaSmith_StripBacktickWrapping(i.TableName) AS TableName,
        SchemaSmith_StripBacktickWrapping(i.IndexName) AS IndexName
    FROM _SchemaSmith_Indexes i
    JOIN INFORMATION_SCHEMA.STATISTICS s
        ON BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
        AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
        AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
        AND s.SEQ_IN_INDEX = 1
    WHERE i.IsPrimaryKey = 0
      -- Skip indexes that were just renamed
      AND NOT EXISTS (
          SELECT 1 FROM _SchemaSmith_IndexRenames r
          WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
            AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
      )
      -- Check if definition differs
      AND (
          -- Columns differ
          BINARY SchemaSmith_NormalizeIndexColumns(i.IndexColumns) != BINARY (
              SELECT GROUP_CONCAT(
                  CONCAT('`', sc.COLUMN_NAME, '`',
                         CASE WHEN BINARY sc.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
                  ORDER BY sc.SEQ_IN_INDEX
                  SEPARATOR ','
              )
              FROM INFORMATION_SCHEMA.STATISTICS sc
              WHERE BINARY sc.TABLE_SCHEMA = BINARY p_DatabaseName
                AND BINARY sc.TABLE_NAME = BINARY s.TABLE_NAME
                AND BINARY sc.INDEX_NAME = BINARY s.INDEX_NAME
          )
          -- Or uniqueness differs
          OR i.IsUnique != (s.NON_UNIQUE = 0)
          -- Or visibility differs (FULLTEXT indexes don't support INVISIBLE, skip them)
          OR (BINARY UPPER(s.INDEX_TYPE) != BINARY 'FULLTEXT'
              AND i.IsVisible != (CASE WHEN s.IS_VISIBLE = 'YES' THEN 1 ELSE 0 END))
      );

    -- Drop modified indexes (they'll be recreated later)
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_ModifiedIndexes;
    ELSE
        BEGIN
            DECLARE v_ModDone INT DEFAULT FALSE;
            DECLARE v_ModTable VARCHAR(128);
            DECLARE v_ModIndex VARCHAR(128);
            DECLARE v_ModSql TEXT;

            DECLARE cur_ModifiedIndexes CURSOR FOR
                SELECT TableName, IndexName
                FROM _SchemaSmith_ModifiedIndexes;

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_ModDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');
            SET v_ModDone = FALSE;
            OPEN cur_ModifiedIndexes;

            drop_modified_loop: LOOP
                FETCH cur_ModifiedIndexes INTO v_ModTable, v_ModIndex;
                IF v_ModDone THEN
                    LEAVE drop_modified_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop and recreate index: ', v_ModTable, '.', v_ModIndex));
                SET v_ModSql = CONCAT('DROP INDEX `', v_ModIndex, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', v_ModTable, '`');
                SET @exec_sql = v_ModSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_ModifiedIndexes;
        END;
    END IF;

    -- =========================================================================
    -- STEP 3: Create missing indexes (non-primary)
    -- =========================================================================
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT(
                      'CREATE ',
                      CASE WHEN UPPER(i.IndexType) = 'SPATIAL' THEN 'SPATIAL '
                           WHEN i.IsUnique = 1 AND i.IsPrimaryKey = 0 THEN 'UNIQUE '
                           ELSE '' END,
                      'INDEX ', i.IndexName,
                      ' ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', i.TableName,
                      ' (', i.IndexColumns, ')',
                      CASE WHEN UPPER(i.IndexType) = 'HASH' THEN ' USING HASH'
                           WHEN UPPER(i.IndexType) = 'BTREE' THEN ' USING BTREE'
                           ELSE '' END)
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          -- Not already renamed
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND r.NewIndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
              WHERE CONVERT(s.TABLE_SCHEMA USING utf8mb4) COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                AND CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          );
    ELSE
        BEGIN
            DECLARE v_CreateDone INT DEFAULT FALSE;
            DECLARE v_CreateTable VARCHAR(128);
            DECLARE v_CreateIndex VARCHAR(128);
            DECLARE v_CreateVariant VARCHAR(128);
            DECLARE v_CreateSql TEXT;

            DECLARE cur_MissingIndexes CURSOR FOR
                SELECT
                    i.TableName,
                    i.IndexName,
                    i.VariantName,
                    CONCAT(
                        'CREATE ',
                        CASE WHEN UPPER(i.IndexType) = 'SPATIAL' THEN 'SPATIAL '
                             WHEN i.IsUnique = 1 AND i.IsPrimaryKey = 0 THEN 'UNIQUE '
                             ELSE '' END,
                        'INDEX ', i.IndexName,
                        ' ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', i.TableName,
                        ' (', i.IndexColumns, ')',
                        CASE WHEN UPPER(i.IndexType) = 'HASH' THEN ' USING HASH'
                             WHEN UPPER(i.IndexType) = 'BTREE' THEN ' USING BTREE'
                             ELSE '' END,
                        CASE WHEN i.IsVisible = 0 THEN ' INVISIBLE' ELSE '' END
                    ) AS CreateIndexStatement
                FROM _SchemaSmith_Indexes i
                WHERE i.IsPrimaryKey = 0
                  AND NOT EXISTS (
                      SELECT 1 FROM _SchemaSmith_IndexRenames r
                      WHERE r.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                        AND r.NewIndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                      WHERE CONVERT(s.TABLE_SCHEMA USING utf8mb4) COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
                        AND CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                        AND CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
                  );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_CreateDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');
            SET v_CreateDone = FALSE;
            OPEN cur_MissingIndexes;

            create_indexes_loop: LOOP
                FETCH cur_MissingIndexes INTO v_CreateTable, v_CreateIndex, v_CreateVariant, v_CreateSql;
                IF v_CreateDone THEN
                    LEAVE create_indexes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create index: ', v_CreateTable, '.', v_CreateIndex,
                    CASE WHEN COALESCE(v_CreateVariant, '') <> '' THEN CONCAT(' (variant: ', v_CreateVariant, ')') ELSE '' END));
                SET @exec_sql = v_CreateSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_MissingIndexes;
        END;
    END IF;

    -- =========================================================================
    -- STEP 4: Create missing check constraints (MySQL 8.0.16+)
    -- =========================================================================
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing check constraints');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' ADD CONSTRAINT ', c.ConstraintName,
                      ' CHECK (', c.Expression, ')')
        FROM _SchemaSmith_CheckConstraints c
        WHERE NOT EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );
    ELSE
        BEGIN
            DECLARE v_CheckDone INT DEFAULT FALSE;
            DECLARE v_CheckTable VARCHAR(128);
            DECLARE v_CheckName VARCHAR(128);
            DECLARE v_CheckVariant VARCHAR(128);
            DECLARE v_CheckSql TEXT;

            DECLARE cur_MissingCheckConstraints CURSOR FOR
                SELECT
                    c.TableName,
                    c.ConstraintName,
                    c.VariantName,
                    CONCAT(
                        'ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                        ' ADD CONSTRAINT ', c.ConstraintName,
                        ' CHECK (', c.Expression, ')'
                    ) AS CreateCheckConstraintStatement
                FROM _SchemaSmith_CheckConstraints c
                WHERE NOT EXISTS (
                    SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                    WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
                      AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
                      AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
                      AND tc.CONSTRAINT_TYPE = 'CHECK'
                );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_CheckDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing check constraints');
            SET v_CheckDone = FALSE;
            OPEN cur_MissingCheckConstraints;

            create_checks_loop: LOOP
                FETCH cur_MissingCheckConstraints INTO v_CheckTable, v_CheckName, v_CheckVariant, v_CheckSql;
                IF v_CheckDone THEN
                    LEAVE create_checks_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create check constraint: ', v_CheckTable, '.', v_CheckName,
                    CASE WHEN COALESCE(v_CheckVariant, '') <> '' THEN CONCAT(' (variant: ', v_CheckVariant, ')') ELSE '' END));
                SET @exec_sql = v_CheckSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_MissingCheckConstraints;
        END;
    END IF;

    -- =========================================================================
    -- STEP 7: Update ProductOwnership for managed objects
    -- =========================================================================
    IF p_WhatIf = 0 THEN
        -- Track indexes
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'INDEX',
               CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
        FROM _SchemaSmith_Indexes i
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
            WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
              AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
        );

        -- Track check constraints
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'CHECK CONSTRAINT',
               CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName))
        FROM _SchemaSmith_CheckConstraints c
        WHERE EXISTS (
            SELECT 1 FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
              AND tc.CONSTRAINT_TYPE = 'CHECK'
        );
    END IF;

    -- =========================================================================
    -- STEP 8: Drop unknown indexes (owned by product but not in definition)
    -- =========================================================================
    IF p_DropUnknownIndexes = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_IndexesToDrop (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            IsUnique TINYINT DEFAULT 0,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Find indexes owned by product but not in current definition
        -- IMPORTANT: Only consider indexes on tables that ARE in the current definition (_SchemaSmith_Tables)
        -- This prevents dropping indexes on tables not included in the current JSON
        INSERT INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
        SELECT
            SUBSTRING_INDEX(po.ObjectName, '.', 1) AS TableName,
            SUBSTRING_INDEX(po.ObjectName, '.', -1) AS IndexName,
            COALESCE(s.NON_UNIQUE = 0, 0) AS IsUnique
        FROM SchemaSmith_ProductOwnership po
        -- Join with _SchemaSmith_Tables to only consider indexes on tables in the current definition
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
        LEFT JOIN INFORMATION_SCHEMA.STATISTICS s
            ON CONVERT(s.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(s.TABLE_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(s.INDEX_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            AND s.SEQ_IN_INDEX = 1
        LEFT JOIN _SchemaSmith_Indexes i
            ON CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
          -- Never drop PRIMARY KEY
          AND UPPER(SUBSTRING_INDEX(po.ObjectName, '.', -1) COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition (LEFT JOIN produces NULL when not found)
          AND i.IndexName IS NULL
          -- Not a renamed index
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName = SUBSTRING_INDEX(po.ObjectName, '.', 1)
                AND r.OldIndexName = SUBSTRING_INDEX(po.ObjectName, '.', -1)
          )
          -- Verify index actually exists
          AND EXISTS (
              SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s2
              WHERE CONVERT(s2.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                AND CONVERT(s2.TABLE_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(s2.INDEX_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
          );

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
            FROM _SchemaSmith_IndexesToDrop;
        ELSE
            -- First, drop any foreign keys that reference unique indexes we're about to drop
            BEGIN
                DECLARE v_FKDropDone INT DEFAULT FALSE;
                DECLARE v_FKDropSql TEXT;
                DECLARE cur_FKsToDropForIndexes CURSOR FOR
                    SELECT CONCAT('ALTER TABLE `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                                  CONVERT(kcu.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                                  '` DROP FOREIGN KEY `', CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`') AS DropFKSql
                    FROM _SchemaSmith_IndexesToDrop itd
                    JOIN INFORMATION_SCHEMA.STATISTICS s
                        ON CONVERT(s.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                        AND CONVERT(s.TABLE_NAME USING utf8mb4) = CONVERT(itd.TableName USING utf8mb4)
                        AND CONVERT(s.INDEX_NAME USING utf8mb4) = CONVERT(itd.IndexName USING utf8mb4)
                        AND s.SEQ_IN_INDEX = 1
                    JOIN INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                        ON CONVERT(kcu.REFERENCED_TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
                        AND CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4) = CONVERT(itd.TableName USING utf8mb4)
                    JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
                        ON CONVERT(tc.TABLE_SCHEMA USING utf8mb4) = CONVERT(kcu.TABLE_SCHEMA USING utf8mb4)
                        AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(kcu.TABLE_NAME USING utf8mb4)
                        AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4)
                        AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY'
                    WHERE itd.IsUnique = 1;

                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_FKDropDone = TRUE;

                SET v_FKDropDone = FALSE;
                OPEN cur_FKsToDropForIndexes;

                drop_fks_for_indexes_loop: LOOP
                    FETCH cur_FKsToDropForIndexes INTO v_FKDropSql;
                    IF v_FKDropDone THEN
                        LEAVE drop_fks_for_indexes_loop;
                    END IF;

                    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop FK for index: ', v_FKDropSql));
                    SET @exec_sql = v_FKDropSql;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                END LOOP;

                CLOSE cur_FKsToDropForIndexes;
            END;

            -- Now drop the unknown indexes
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');
            BEGIN
                DECLARE v_DropDone INT DEFAULT FALSE;
                DECLARE v_DropTable VARCHAR(128);
                DECLARE v_DropIndex VARCHAR(128);
                DECLARE v_DropSql TEXT;
                DECLARE cur_DropIndexes CURSOR FOR
                    SELECT TableName, IndexName, CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`') AS DropIndexSql
                    FROM _SchemaSmith_IndexesToDrop;

                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_DropDone = TRUE;

                SET v_DropDone = FALSE;
                OPEN cur_DropIndexes;

                drop_indexes_loop: LOOP
                    FETCH cur_DropIndexes INTO v_DropTable, v_DropIndex, v_DropSql;
                    IF v_DropDone THEN
                        LEAVE drop_indexes_loop;
                    END IF;

                    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop unknown index: ', v_DropTable, '.', v_DropIndex));
                    SET @exec_sql = v_DropSql;
                    PREPARE stmt FROM @exec_sql;
                    EXECUTE stmt;
                    DEALLOCATE PREPARE stmt;
                END LOOP;

                CLOSE cur_DropIndexes;
            END;

            -- Remove dropped indexes from ProductOwnership
            DELETE po FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_IndexesToDrop itd
                ON po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(itd.TableName, '.', itd.IndexName) COLLATE utf8mb4_unicode_ci
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
    END IF;

    -- Cleanup temporary tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenames;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedIndexes;

END//

DELIMITER ;
