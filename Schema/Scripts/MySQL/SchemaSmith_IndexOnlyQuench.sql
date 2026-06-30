-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

DROP PROCEDURE IF EXISTS SchemaSmith_IndexOnlyQuench//

CREATE PROCEDURE SchemaSmith_IndexOnlyQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT,
    IN p_DropIndexesRemovedFromProduct TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure handles index-only quenching.
    -- It creates, modifies, and drops indexes but does NOT touch:
    -- - Table structure (columns, data types)
    -- - Foreign keys
    -- - Check constraints
    --
    -- It reads from the _SchemaSmith_Indexes and _SchemaSmith_FullTextIndexes
    -- temp tables populated by ParseTableJson.

    DECLARE v_Done INT DEFAULT FALSE;
    DECLARE v_Sql TEXT;
    DECLARE v_TableName VARCHAR(128);
    DECLARE v_IndexName VARCHAR(128);
    DECLARE v_Variant VARCHAR(128);

    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'BEGIN IndexOnlyQuench');

    -- Ensure _SchemaSmith_FullTextIndexes exists (ParseTableJson may not create it if there are no fulltext indexes).
    -- Keep this fallback schema in lockstep with ParseTableJson's primary definition (see
    -- SchemaSmith_ParseTableJson.sql) — both use a synthetic RowId so two same-named entries
    -- with mutually exclusive ShouldApplyExpression can coexist until the ShouldApply DELETE pass.
    CREATE TEMPORARY TABLE IF NOT EXISTS _SchemaSmith_FullTextIndexes (
        RowId INT AUTO_INCREMENT NOT NULL PRIMARY KEY,
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        Columns TEXT NOT NULL,
        Parser VARCHAR(128) DEFAULT NULL,
        Comment VARCHAR(255) DEFAULT NULL,
        VariantName VARCHAR(128) DEFAULT NULL,
        KEY ix_ft_table_name (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

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
                -- MySQL 8.0+ supports ALTER TABLE ... RENAME INDEX
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
      -- Check if definition differs (columns, uniqueness, or index type)
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
          -- Or index type differs (BTREE vs HASH)
          OR (BINARY UPPER(COALESCE(i.IndexType, 'BTREE')) != BINARY UPPER(s.INDEX_TYPE)
              AND NOT (BINARY UPPER(COALESCE(i.IndexType, 'BTREE')) = BINARY 'BTREE' AND BINARY UPPER(s.INDEX_TYPE) = BINARY 'BTREE'))
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
    -- STEP 3: Drop unknown indexes (owned by product but not in definition)
    -- =========================================================================
    IF p_DropUnknownIndexes = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_IndexesToDrop (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            IsUnique TINYINT DEFAULT 0,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Create helper temp table to copy index definitions
        -- MySQL cannot reference the same temporary table multiple times in a query
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedIndexes;
        CREATE TEMPORARY TABLE _SchemaSmith_DefinedIndexes (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Populate with table.index pairs from the definition
        INSERT INTO _SchemaSmith_DefinedIndexes (TableName, IndexName)
        SELECT CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4),
               CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4)
        FROM _SchemaSmith_Indexes i;

        -- Find indexes owned by product but not in current definition
        -- IMPORTANT: Only consider indexes on tables that ARE in the current definition
        -- This prevents IndexOnlyQuench from dropping indexes on tables not included in the JSON
        -- NOTE: _SchemaSmith_Tables is populated by ParseTableJson with ALL tables from JSON
        INSERT INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
        SELECT DISTINCT
            SUBSTRING_INDEX(po.ObjectName, '.', 1) AS TableName,
            SUBSTRING_INDEX(po.ObjectName, '.', -1) AS IndexName,
            COALESCE(s.NON_UNIQUE = 0, 0) AS IsUnique
        FROM SchemaSmith_ProductOwnership po
        -- Join with _SchemaSmith_Tables to only consider indexes on tables in the current JSON
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
        -- Left join with defined indexes to find which are NOT defined
        LEFT JOIN _SchemaSmith_DefinedIndexes di
            ON CONVERT(di.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        LEFT JOIN INFORMATION_SCHEMA.STATISTICS s
            ON CONVERT(s.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND CONVERT(s.TABLE_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(s.INDEX_NAME USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            AND s.SEQ_IN_INDEX = 1
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = p_ProductName COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = p_DatabaseName COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = 'INDEX' COLLATE utf8mb4_unicode_ci
          -- Removed-from-product per-table tightening (the outer IF still gates on DropUnknownIndexes;
          -- that mis-gating is normalized in Index-B). p_DropIndexesRemovedFromProduct defaults on, so
          -- this only adds suppression, no default behavior change.
          AND p_DropIndexesRemovedFromProduct = 1
          AND COALESCE(t.DropIndexesRemovedFromProduct, 1) = 1
          -- Never drop PRIMARY KEY
          AND UPPER(SUBSTRING_INDEX(po.ObjectName, '.', -1) COLLATE utf8mb4_unicode_ci) != 'PRIMARY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition (LEFT JOIN produces NULL when not found)
          AND di.IndexName IS NULL
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

        -- Cleanup helper temp table
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DefinedIndexes;

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
            FROM _SchemaSmith_IndexesToDrop;
        ELSE
            -- First, drop any foreign keys that reference unique indexes we're about to drop
            BEGIN
                DECLARE v_FKDone INT DEFAULT FALSE;
                DECLARE v_FKSql TEXT;
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

                DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_FKDone = TRUE;

                SET v_FKDone = FALSE;
                OPEN cur_FKsToDropForIndexes;

                drop_fks_for_indexes_loop: LOOP
                    FETCH cur_FKsToDropForIndexes INTO v_FKSql;
                    IF v_FKDone THEN
                        LEAVE drop_fks_for_indexes_loop;
                    END IF;

                    INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop FK for index: ', v_FKSql));
                    SET @exec_sql = v_FKSql;
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
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = 'INDEX' COLLATE utf8mb4_unicode_ci;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
    END IF;

    -- =========================================================================
    -- STEP 4: Create missing indexes (non-primary)
    -- =========================================================================
    BEGIN
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
              -- Not already renamed to this name
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_IndexRenames r
                  WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                    AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
              )
              -- Doesn't exist yet
              AND NOT EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                  WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                    AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
              );

        DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_Done = TRUE;

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
                               ELSE '' END,
                          CASE WHEN i.IsVisible = 0 THEN ' INVISIBLE' ELSE '' END)
            FROM _SchemaSmith_Indexes i
            WHERE i.IsPrimaryKey = 0
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_IndexRenames r
                  WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                    AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                  WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
                    AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
              );
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');
            SET v_Done = FALSE;
            OPEN cur_MissingIndexes;

            create_indexes_loop: LOOP
                FETCH cur_MissingIndexes INTO v_TableName, v_IndexName, v_Variant, v_Sql;
                IF v_Done THEN
                    LEAVE create_indexes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create index: ', v_TableName, '.', v_IndexName,
                    CASE WHEN COALESCE(v_Variant, '') <> '' THEN CONCAT(' (variant: ', v_Variant, ')') ELSE '' END));
                SET @exec_sql = v_Sql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_MissingIndexes;
        END IF;
    END;

    -- =========================================================================
    -- STEP 5: Update ProductOwnership for managed indexes
    -- =========================================================================
    IF p_WhatIf = 0 THEN
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
    END IF;

    -- =========================================================================
    -- STEP 6: Handle fulltext indexes
    -- =========================================================================
    -- Drop fulltext indexes that no longer exist in definition
    IF p_DropUnknownIndexes = 1 AND p_WhatIf = 0 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown fulltext indexes');
        BEGIN
            DECLARE v_FTDropDone INT DEFAULT FALSE;
            DECLARE v_FTDropSql TEXT;
            DECLARE cur_DropFullText CURSOR FOR
                SELECT CONCAT('DROP INDEX `', CONVERT(s.INDEX_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
                             '` ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.`',
                             CONVERT(s.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`')
                FROM INFORMATION_SCHEMA.STATISTICS s
                WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                  AND s.INDEX_TYPE = 'FULLTEXT'
                  AND s.SEQ_IN_INDEX = 1
                  AND EXISTS (
                      SELECT 1 FROM SchemaSmith_ProductOwnership po
                      WHERE BINARY po.ProductName = BINARY p_ProductName
                        AND BINARY po.ObjectSchema = BINARY p_DatabaseName
                        AND po.ObjectType = 'INDEX'
                        AND BINARY po.ObjectName = BINARY CONCAT(CONVERT(s.TABLE_NAME USING utf8mb4), '.', CONVERT(s.INDEX_NAME USING utf8mb4))
                  )
                  AND NOT EXISTS (
                      SELECT 1 FROM _SchemaSmith_FullTextIndexes ft
                      WHERE BINARY SchemaSmith_StripBacktickWrapping(ft.TableName) = BINARY s.TABLE_NAME
                        AND BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName) = BINARY s.INDEX_NAME
                  );

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_FTDropDone = TRUE;

            SET v_FTDropDone = FALSE;
            OPEN cur_DropFullText;

            drop_fulltext_loop: LOOP
                FETCH cur_DropFullText INTO v_FTDropSql;
                IF v_FTDropDone THEN
                    LEAVE drop_fulltext_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Drop unknown fulltext index: ', v_FTDropSql));
                SET @exec_sql = v_FTDropSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_DropFullText;
        END;
    END IF;

    -- Create missing fulltext indexes
    BEGIN
        DECLARE v_FTDone INT DEFAULT FALSE;
        DECLARE v_FTTable VARCHAR(128);
        DECLARE v_FTIndex VARCHAR(128);
        DECLARE v_FTVariant VARCHAR(128);
        DECLARE v_FTSql TEXT;

        DECLARE cur_MissingFullText CURSOR FOR
            SELECT
                ft.TableName,
                ft.IndexName,
                ft.VariantName,
                CONCAT(
                    'CREATE FULLTEXT INDEX ', ft.IndexName,
                    ' ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', ft.TableName,
                    ' (', ft.Columns, ')',
                    CASE WHEN ft.Comment IS NOT NULL AND ft.Comment != ''
                         THEN CONCAT(' COMMENT ''', REPLACE(ft.Comment, '''', ''''''), '''')
                         ELSE '' END,
                    CASE WHEN ft.Parser IS NOT NULL AND ft.Parser != ''
                         THEN CONCAT(' WITH PARSER ', ft.Parser)
                         ELSE '' END
                ) AS CreateFullTextStatement
            FROM _SchemaSmith_FullTextIndexes ft
            WHERE NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                  AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.TableName)
                  AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName)
                  AND s.INDEX_TYPE = 'FULLTEXT'
            );

        DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_FTDone = TRUE;

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing fulltext indexes');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT(
                          'CREATE FULLTEXT INDEX ', ft.IndexName,
                          ' ON `', p_DatabaseName COLLATE utf8mb4_unicode_ci, '`.', ft.TableName,
                          ' (', ft.Columns, ')',
                          CASE WHEN ft.Comment IS NOT NULL AND ft.Comment != ''
                               THEN CONCAT(' COMMENT ''', REPLACE(ft.Comment, '''', ''''''), '''')
                               ELSE '' END,
                          CASE WHEN ft.Parser IS NOT NULL AND ft.Parser != ''
                               THEN CONCAT(' WITH PARSER ', ft.Parser)
                               ELSE '' END)
            FROM _SchemaSmith_FullTextIndexes ft
            WHERE NOT EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                  AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.TableName)
                  AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName)
                  AND s.INDEX_TYPE = 'FULLTEXT'
            );
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing fulltext indexes');
            SET v_FTDone = FALSE;
            OPEN cur_MissingFullText;

            create_fulltext_loop: LOOP
                FETCH cur_MissingFullText INTO v_FTTable, v_FTIndex, v_FTVariant, v_FTSql;
                IF v_FTDone THEN
                    LEAVE create_fulltext_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Create fulltext index: ', v_FTTable, '.', v_FTIndex,
                    CASE WHEN COALESCE(v_FTVariant, '') <> '' THEN CONCAT(' (variant: ', v_FTVariant, ')') ELSE '' END));
                SET @exec_sql = v_FTSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_MissingFullText;

            -- Track fulltext indexes in ProductOwnership
            INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
            SELECT p_ProductName, '', p_DatabaseName, 'INDEX',
                   CONCAT(SchemaSmith_StripBacktickWrapping(ft.TableName), '.', SchemaSmith_StripBacktickWrapping(ft.IndexName))
            FROM _SchemaSmith_FullTextIndexes ft
            WHERE EXISTS (
                SELECT 1 FROM INFORMATION_SCHEMA.STATISTICS s
                WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
                  AND BINARY s.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.TableName)
                  AND BINARY s.INDEX_NAME = BINARY SchemaSmith_StripBacktickWrapping(ft.IndexName)
            );
        END IF;
    END;

    -- Cleanup temporary tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenames;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedIndexes;

END//

DELIMITER ;
