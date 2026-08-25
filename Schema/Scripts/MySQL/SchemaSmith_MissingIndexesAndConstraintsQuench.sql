-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DELIMITER //

-- NAME UNDERSELLS THIS PROCEDURE: it also performs INDEX REMOVAL on MySQL/MariaDB
-- (DropIndexesRemovedFromProduct), which SQL Server and PostgreSQL do in ModifiedTableQuench instead.
-- Reading ModifiedTableQuench's signature and concluding MySQL cannot remove indexes is the specific
-- mistake this comment exists to prevent -- it was made six times in one audit, in both directions.
DROP PROCEDURE IF EXISTS SchemaSmith_MissingIndexesAndConstraintsQuench//

CREATE PROCEDURE SchemaSmith_MissingIndexesAndConstraintsQuench(
    IN p_ProductName VARCHAR(100),
    IN p_DatabaseName VARCHAR(128),
    IN p_WhatIf TINYINT,
    IN p_DropUnknownIndexes TINYINT,
    IN p_DropCheckConstraintsRemovedFromProduct TINYINT,
    IN p_DropIndexesRemovedFromProduct TINYINT
)
SQL SECURITY DEFINER
BEGIN
    -- This procedure creates, modifies, renames, and drops indexes and check constraints.
    -- It reads from the _SchemaSmith_Indexes and _SchemaSmith_CheckConstraints
    -- temp tables populated by ParseTableJson.
    -- Foreign keys are handled separately by SchemaSmith_ForeignKeyQuench.
    --
    -- Execution model: each per-row DDL loop materializes its statements (plus the exact
    -- per-object progress message) into an AUTO_INCREMENT temp table, then a WHILE loop
    -- drains it by ascending RowId (SELECT ... INTO + PREPARE/EXECUTE). This replaces the
    -- old cursors and matches the materialize-then-WHILE idiom used by ForeignKeyQuench.
    -- Detection reads INFORMATION_SCHEMA through STEP-LOCAL snapshots, not per declared object.
    -- INFORMATION_SCHEMA is not a stored table on MySQL/MariaDB, so a correlated / per-row read
    -- re-materialises server-wide metadata once per object (cost = declared objects x tables-on-instance);
    -- the same shape ForeignKeyQuench eliminated. Each detection pass instead snapshots the catalog it
    -- needs into a temp table with ONE scan, then joins it. The snapshots are placed to preserve the
    -- exact point-in-time state each pass must see relative to THIS proc's own earlier mutations:
    --   * _SchemaSmith_IdxDetectSnap + _SchemaSmith_ExistingIndexVisibility -- pre-mutation, for STEP 1
    --     (rename) and STEP 2 (modified index); STEP 1 renames only where columns already match and STEP 2
    --     excludes just-renamed indexes, so the pre-mutation snapshot equals the live reads it replaces.
    --   * _SchemaSmith_IdxExistPostDrop -- taken after STEP 2's drops so STEP 3 sees a dropped modified
    --     index as MISSING and recreates it.
    --   * _SchemaSmith_ChkExist -- built after the STEP 3.5 / by-absence check drops for the STEP 4/4.5
    --     create passes, then REBUILT post-create for STEP 7 ownership.
    --   * _SchemaSmith_IdxExistFinal + the STEP 7 _SchemaSmith_ChkExist rebuild -- post-create, so STEP 7
    --     writes ownership only for objects that actually landed.
    -- In WhatIf mode nothing is mutated, so every snapshot reflects the unchanged catalog -- identical to
    -- the live reads. STEP 8 keeps its own late snapshots (STATISTICS / KEY_COLUMN_USAGE / TABLE_CONSTRAINTS),
    -- the ProductOwnership x STATISTICS query the roadmap flags as a frequent-run segfault trigger.

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

        -- #363: WhatIf twin of the ELSE-branch generated-column 'created' audit; same source, same
        -- AuditName form as _SchemaSmith_GenColStmts.AuditName.
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'column', CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'wouldCreate'
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.GeneratedExpression IS NOT NULL
          AND TRIM(c.GeneratedExpression) != ''
          AND c.NewColumn = 1;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Add missing generated columns');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_GenColStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_GenColStmts (LogMsg, Stmt, AuditName)
        SELECT
            CONCAT('  Add generated column: ',
                   CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                          ' ADD COLUMN ', c.ColumnScript),
                   CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END),
            CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                   ' ADD COLUMN ', c.ColumnScript),
            CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        FROM _SchemaSmith_Columns c
        INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
        WHERE c.GeneratedExpression IS NOT NULL
          AND TRIM(c.GeneratedExpression) != ''
          AND c.NewColumn = 1
        ORDER BY c.TableName, c.DependencyLevel, c.OrdinalPosition;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenColStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_GenColStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'column', @ss_auditname, 'created');
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_GenColStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColStmts;
    END IF;

    -- =========================================================================
    -- STEP 0.5: Degrade descending index key parts below MySQL 8.0 / MariaDB 10.8
    -- =========================================================================
    -- These engines parse-and-ignore a DESC key part (silently storing it ascending). There is no
    -- equivalent, so a declared DESC index is stored + compared as ascending (SchemaSmith_NormalizeIndexColumns
    -- drops the DESC suffix below the floor, so the create/compare steps below see an ascending index and stay
    -- idempotent). Record one 'downgraded' manifest row + a run-log line per affected index so the downgrade is
    -- visible, mirroring the CHECK-constraint degrade. At/above the floor this is a no-op.
    IF SchemaSmith_SupportsDescendingIndex() = 0 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Descending index key part stored ascending (requires MySQL 8.0 / MariaDB 10.8 - downgraded): ',
               SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
        FROM _SchemaSmith_Indexes i
        WHERE UPPER(CONVERT(i.IndexColumns USING utf8mb4)) LIKE '% DESC%';
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'INDEX (descending key part, MySQL 8.0 / MariaDB 10.8)',
               CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName)), 'downgraded'
        FROM _SchemaSmith_Indexes i
        WHERE UPPER(CONVERT(i.IndexColumns USING utf8mb4)) LIKE '% DESC%';
    END IF;

    -- =========================================================================
    -- STEP 0.6: Degrade invisible indexes below MySQL 8.0 / MariaDB 10.6
    -- =========================================================================
    -- The INVISIBLE (MySQL) / IGNORED (MariaDB) keyword is a hard syntax error below these versions (unlike a
    -- DESC key part, which parses-and-ignores). A declared invisible index degrades: the visibility clause is
    -- suppressed (see the create pass) and the modified-index compare ignores the visibility difference so the
    -- deploy stays idempotent. 'fail' aborts naming the offending index(es); 'warn' (default) records one
    -- 'downgraded' manifest row + a run-log line per declared invisible index. At/above the floor this is a no-op.
    IF SchemaSmith_SupportsInvisibleIndex() = 0
       AND EXISTS (SELECT 1 FROM _SchemaSmith_Indexes WHERE IsVisible = 0) THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail' THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Invisible index requires MySQL 8.0 / MariaDB 10.6 (UnsupportedFeaturePolicy=fail): ',
                   SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
            FROM _SchemaSmith_Indexes i WHERE i.IsVisible = 0;
            SET @ss_msg = 'Invisible index requires MySQL 8.0 / MariaDB 10.6 (UnsupportedFeaturePolicy=fail). See the run log for the full list.';
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Invisible index stored visible (requires MySQL 8.0 / MariaDB 10.6 - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
            FROM _SchemaSmith_Indexes i WHERE i.IsVisible = 0;
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'INDEX (invisible, MySQL 8.0 / MariaDB 10.6)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName)), 'downgraded'
            FROM _SchemaSmith_Indexes i WHERE i.IsVisible = 0;
        END IF;
    END IF;

    -- =========================================================================
    -- STEP 0.7: Degrade functional/expression indexes below MySQL 8.0.13 (MariaDB: always)
    -- =========================================================================
    -- Unlike a DESC key part (parsed-and-ignored) or INVISIBLE (a suppressible clause), a functional/
    -- expression key part is a hard syntax error below the floor -- there is no reduced form to fall
    -- back to, so the whole index is skipped rather than degraded in place (same shape as
    -- SchemaSmith_SupportsDefaultExpression's column skip). MariaDB has no equivalent in this form at
    -- ANY version (SchemaSmith_SupportsFunctionalIndex() is unconditionally 0 there), so this block
    -- fires on MariaDB every time a functional index is declared, not just below some threshold -- the
    -- mirror image of SchemaSmith_SupportsDefaultExpression, which is unconditionally 1 on MariaDB. A
    -- multi-valued index (CAST(... AS ... ARRAY)) is a functional key part too and rides this same gate;
    -- see SchemaSmith_SupportsFunctionalIndex for why it needs no gate of its own. 'fail' aborts naming
    -- the offending index(es); 'warn' (default) records one 'downgraded' manifest row + a run-log line
    -- per declared functional index -- STEP 2 (modified-detect) and STEP 3 (create) below exclude it via
    -- the identical predicate, leaving any live index of the same name untouched.
    -- =========================================================================
    IF SchemaSmith_SupportsFunctionalIndex() = 0
       AND EXISTS (SELECT 1 FROM _SchemaSmith_Indexes i
                   WHERE i.IsPrimaryKey = 0 AND SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1) THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail' THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Functional/expression index requires MySQL 8.0.13 (MariaDB unsupported) (UnsupportedFeaturePolicy=fail): ',
                   SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
            FROM _SchemaSmith_Indexes i
            WHERE i.IsPrimaryKey = 0 AND SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1;
            SET @ss_msg = 'Functional/expression index requires MySQL 8.0.13; MariaDB unsupported (policy=fail). See the run log.';
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Skipping index (functional/expression key part requires MySQL 8.0.13, MariaDB unsupported - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
            FROM _SchemaSmith_Indexes i
            WHERE i.IsPrimaryKey = 0 AND SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1;
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'INDEX (functional/expression, MySQL 8.0.13)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName)), 'downgraded'
            FROM _SchemaSmith_Indexes i
            WHERE i.IsPrimaryKey = 0 AND SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1;
        END IF;
    END IF;

    -- =========================================================================
    -- Detection snapshot: the live index picture read ONCE here into a temp table so STEP 1 (rename)
    -- and STEP 2 (modified) join it instead of re-reading INFORMATION_SCHEMA.STATISTICS per declared
    -- index. INFORMATION_SCHEMA is not a stored table on MySQL/MariaDB -- each access re-materialises
    -- server-wide metadata -- so the original per-row reads (a STATISTICS join plus a correlated
    -- GROUP_CONCAT column-list subquery, per declared index) cost (declared indexes x whole-server
    -- scans). This snapshot reflects the catalog BEFORE STEP 1's renames and STEP 2's drops, which is
    -- exactly the state both passes read: STEP 1 renames only where the column list already matches
    -- (so it never changes what STEP 2 compares), and STEP 2 excludes just-renamed indexes via
    -- _SchemaSmith_IndexRenames -- so the pre-mutation snapshot is equivalent to the live reads it
    -- replaces. STEP 3 (create-missing) needs the POST-drop state and takes its own later snapshot.
    -- One row per index; NormColumns is built by the same GROUP_CONCAT expression the correlated
    -- subqueries used, so composite index column lists compare byte-for-byte as before.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxDetectSnap;
    CREATE TEMPORARY TABLE _SchemaSmith_IdxDetectSnap (
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        NonUnique TINYINT DEFAULT 0,
        IndexType VARCHAR(32),
        NormColumns TEXT,
        -- MySQL's index comment ceiling is 1024 characters; MAX() alongside the other per-index
        -- aggregates below since INDEX_COMMENT is constant across a composite index's key parts.
        IndexComment VARCHAR(1024),
        PRIMARY KEY (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    -- A functional/expression key part (MySQL 8.0.13+) has NULL COLUMN_NAME and reports its text via
    -- EXPRESSION instead; that column does not exist below the floor or on MariaDB, so the branch that
    -- reads it is gated behind SchemaSmith_SupportsFunctionalIndex() as two whole statements -- column
    -- resolution is deferred to the execution of whichever statement actually runs (see
    -- SchemaSmith_IndexIsVisible / SchemaSmith_SnapshotIndexVisibility for the same IS_VISIBLE-below-8.0
    -- shape), so the unreached branch's EXPRESSION reference is never bound on an engine that lacks it.
    -- Must produce the exact same per-key-part form as SchemaSmith_NormalizeIndexColumns and
    -- GenerateTableJson (one extra paren pair around the expression, charset-introducer noise AND
    -- the backslash-escaped quotes EXPRESSION carries around a literal's quotes both stripped the
    -- same two-pass way via REGEXP_REPLACE -- safe here despite the 5.7 floor because this branch
    -- only executes at 8.0.13+ and never on 5.7/MariaDB; see GenerateTableJson.sql for the full
    -- explanation) or the compare below never converges.
    IF SchemaSmith_SupportsFunctionalIndex() = 1 THEN
        INSERT INTO _SchemaSmith_IdxDetectSnap (TableName, IndexName, NonUnique, IndexType, NormColumns, IndexComment)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4),
               CONVERT(s.INDEX_NAME USING utf8mb4),
               MAX(s.NON_UNIQUE),
               CONVERT(MAX(s.INDEX_TYPE) USING utf8mb4),
               GROUP_CONCAT(
                   CASE WHEN s.COLUMN_NAME IS NOT NULL THEN
                       CONCAT('`', s.COLUMN_NAME, '`',
                              -- SPATIAL's SUB_PART is a phantom internal value (always 32), not a declared prefix; exclude it or spatial indexes never converge
                              IF(s.SUB_PART IS NOT NULL AND s.INDEX_TYPE != 'SPATIAL', CONCAT('(', s.SUB_PART, ')'), ''),
                              CASE WHEN BINARY s.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
                   ELSE
                       CONCAT('(', REGEXP_REPLACE(
                           REPLACE(s.EXPRESSION, CONCAT(CHAR(92), CHAR(39)), CHAR(39)),
                           '_[A-Za-z0-9]+''', ''''), ')',
                              CASE WHEN BINARY s.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
                   END
                   ORDER BY s.SEQ_IN_INDEX
                   SEPARATOR ','
               ),
               CONVERT(MAX(s.INDEX_COMMENT) USING utf8mb4)
          FROM INFORMATION_SCHEMA.STATISTICS s
         WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
         GROUP BY s.TABLE_NAME, s.INDEX_NAME;
    ELSE
        INSERT INTO _SchemaSmith_IdxDetectSnap (TableName, IndexName, NonUnique, IndexType, NormColumns, IndexComment)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4),
               CONVERT(s.INDEX_NAME USING utf8mb4),
               MAX(s.NON_UNIQUE),
               CONVERT(MAX(s.INDEX_TYPE) USING utf8mb4),
               GROUP_CONCAT(
                   CONCAT('`', s.COLUMN_NAME, '`',
                          IF(s.SUB_PART IS NOT NULL AND s.INDEX_TYPE != 'SPATIAL', CONCAT('(', s.SUB_PART, ')'), ''),
                          CASE WHEN BINARY s.COLLATION = BINARY 'D' THEN ' DESC' ELSE '' END)
                   ORDER BY s.SEQ_IN_INDEX
                   SEPARATOR ','
               ),
               CONVERT(MAX(s.INDEX_COMMENT) USING utf8mb4)
          FROM INFORMATION_SCHEMA.STATISTICS s
         WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
         GROUP BY s.TABLE_NAME, s.INDEX_NAME;
    END IF;

    -- A names-only copy of the same snapshot. STEP 1 references the snapshot twice in one statement
    -- (the main join AND the "new index name doesn't exist" NOT EXISTS); MySQL/MariaDB forbid opening a
    -- TEMPORARY table twice in a single query (ER_CANT_REOPEN_TABLE 1137) -- the original could because
    -- it read INFORMATION_SCHEMA (not a temp) on both sides. The second reference reads this copy.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxDetectNames;
    CREATE TEMPORARY TABLE _SchemaSmith_IdxDetectNames (
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    INSERT INTO _SchemaSmith_IdxDetectNames (TableName, IndexName)
    SELECT TableName, IndexName FROM _SchemaSmith_IdxDetectSnap;

    -- Per-engine index-visibility snapshot (MySQL IS_VISIBLE / MariaDb IGNORED), one scan, for STEP 2's
    -- modified-index visibility comparison -- replaces the per-candidate SchemaSmith_IndexIsVisible() call.
    CALL SchemaSmith_SnapshotIndexVisibility(p_DatabaseName);

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
        snap.IndexName COLLATE utf8mb4_unicode_ci AS OldIndexName,
        SchemaSmith_StripBacktickWrapping(i.IndexName) AS NewIndexName
    FROM _SchemaSmith_Indexes i
    JOIN _SchemaSmith_IdxDetectSnap snap
        ON BINARY snap.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
    JOIN SchemaSmith_ProductOwnership po
        ON BINARY po.ProductName = BINARY p_ProductName
        AND BINARY po.ObjectSchema = BINARY p_DatabaseName
        AND po.ObjectType = 'INDEX'
        AND BINARY po.ObjectName = BINARY CONCAT(snap.TableName, '.', snap.IndexName)
    WHERE i.IsPrimaryKey = 0
      -- New index name doesn't exist
      AND NOT EXISTS (
          SELECT 1 FROM _SchemaSmith_IdxDetectNames s2
          WHERE BINARY s2.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
            AND BINARY s2.IndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
      )
      -- Old index exists with same columns (compare normalized column list)
      AND BINARY SchemaSmith_NormalizeIndexColumns(i.IndexColumns) = BINARY snap.NormColumns
      -- Same uniqueness
      AND i.IsUnique = (snap.NonUnique = 0);

    -- Handle renames
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle index renames');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                      SchemaSmith_BuildIndexRenameClause(p_DatabaseName, TableName, OldIndexName, NewIndexName))
        FROM _SchemaSmith_IndexRenames;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Handle index renames');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_RenameStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_RenameStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_RenameStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Rename index: ', TableName, '.', OldIndexName, ' -> ', NewIndexName),
            CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
                   SchemaSmith_BuildIndexRenameClause(p_DatabaseName, TableName, OldIndexName, NewIndexName))
        FROM _SchemaSmith_IndexRenames;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_RenameStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_RenameStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_RenameStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_RenameStmts;

        -- Update ProductOwnership with the new names. Done set-based after the renames: the
        -- STEP 1 "new name doesn't exist" filter rules out rename chains (a->b where b is itself
        -- a live index), so no old name equals another rename's new name and one pass suffices.
        UPDATE SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_IndexRenames r
            ON BINARY po.ObjectName = BINARY CONCAT(r.TableName, '.', r.OldIndexName)
        SET po.ObjectName = CONCAT(r.TableName, '.', r.NewIndexName)
        WHERE BINARY po.ProductName = BINARY p_ProductName
          AND BINARY po.ObjectSchema = BINARY p_DatabaseName
          AND po.ObjectType = 'INDEX';
    END IF;

    -- STEP 1 executed its renames in the live branch, so each renamed index's OLD name is now gone from
    -- the catalog. Drop those old names from the detection snapshots before STEP 2 -- otherwise a declared
    -- index whose name equals a renamed-away old name (e.g. two indexes on one column, where one is renamed
    -- and the other declared under the freed-up old name) would match a stale snapshot row and be wrongly
    -- flagged as modified, generating a DROP for an index that no longer exists under that name. The
    -- original read live INFORMATION_SCHEMA here, which already reflected the rename. WhatIf executes no
    -- rename, so it must keep the old names to match that live-read behaviour -- hence the p_WhatIf guard.
    IF p_WhatIf = 0 THEN
        DELETE snap FROM _SchemaSmith_IdxDetectSnap snap
            JOIN _SchemaSmith_IndexRenames r
              ON BINARY r.TableName = BINARY snap.TableName AND BINARY r.OldIndexName = BINARY snap.IndexName;
        DELETE nm FROM _SchemaSmith_IdxDetectNames nm
            JOIN _SchemaSmith_IndexRenames r
              ON BINARY r.TableName = BINARY nm.TableName AND BINARY r.OldIndexName = BINARY nm.IndexName;
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
    JOIN _SchemaSmith_IdxDetectSnap snap
        ON BINARY snap.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
        AND BINARY snap.IndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
    LEFT JOIN _SchemaSmith_ExistingIndexVisibility viz
        ON BINARY viz.TableName = BINARY snap.TableName
        AND BINARY viz.IndexName = BINARY snap.IndexName
    WHERE i.IsPrimaryKey = 0
      -- Skip indexes that were just renamed
      AND NOT EXISTS (
          SELECT 1 FROM _SchemaSmith_IndexRenames r
          WHERE BINARY r.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
            AND BINARY r.NewIndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
      )
      -- A declared functional/expression index this target cannot legally CREATE (below the floor / see
      -- the degrade guard above) is left untouched rather than flagged modified-and-dropped: STEP 3 below
      -- excludes it from recreation via the identical predicate, so dropping it here would lose it for good.
      AND NOT (SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1 AND SchemaSmith_SupportsFunctionalIndex() = 0)
      -- Check if definition differs
      AND (
          -- Columns differ
          BINARY SchemaSmith_NormalizeIndexColumns(i.IndexColumns) != BINARY snap.NormColumns
          -- Or uniqueness differs
          OR i.IsUnique != (snap.NonUnique = 0)
          -- Or visibility differs (FULLTEXT indexes don't support INVISIBLE, skip them). Below the
          -- invisible-index floor (MySQL 8.0 / MariaDB 10.6) the keyword can't be emitted, so a declared
          -- invisible index is stored visible; ignore the visibility difference there or it churns every run.
          -- viz.IsVisible is the once-snapshotted per-engine visibility (IS_VISIBLE / IGNORED), replacing
          -- the per-candidate SchemaSmith_IndexIsVisible() read; it is populated only at/above the floor,
          -- which is exactly when SchemaSmith_SupportsInvisibleIndex() = 1 gates this term.
          OR (BINARY UPPER(snap.IndexType) != BINARY 'FULLTEXT'
              AND SchemaSmith_SupportsInvisibleIndex() = 1
              AND i.IsVisible != viz.IsVisible)
          -- Or comment differs (symmetric: covers added, changed, and cleared, matching the column
          -- comment predicate in ModifiedTableQuench). FULLTEXT indexes never reach this file (parsed
          -- separately into _SchemaSmith_FullTextIndexes), so no FULLTEXT exclusion is needed here.
          OR (BINARY COALESCE(snap.IndexComment, '') != BINARY COALESCE(i.Comment, ''))
      );

    -- Drop modified indexes (they'll be recreated later)
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_ModifiedIndexes;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop and recreate modified indexes');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModIdxStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_DropModIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_DropModIdxStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Drop and recreate index: ', TableName, '.', IndexName),
            CONCAT('DROP INDEX `', IndexName, '` ON `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_ModifiedIndexes;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModIdxStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_DropModIdxStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModIdxStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModIdxStmts;
    END IF;

    -- =========================================================================
    -- STEP 3: Create missing indexes (non-primary)
    -- =========================================================================
    -- Post-mutation existence snapshot: taken HERE, after STEP 1's renames and STEP 2's drops, so it
    -- reflects the current catalog -- exactly what the original per-index live NOT EXISTS reads saw at
    -- this point. This is the crux the "snapshot once at top" approach would get wrong: a just-dropped
    -- modified index (STEP 2) must be seen as MISSING here and recreated, so this snapshot must be
    -- taken after the drops, not reused from the pre-STEP-1 detection snapshot. One row per index.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxExistPostDrop;
    CREATE TEMPORARY TABLE _SchemaSmith_IdxExistPostDrop (
        TableName VARCHAR(128) NOT NULL,
        IndexName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, IndexName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    INSERT INTO _SchemaSmith_IdxExistPostDrop (TableName, IndexName)
    SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4)
    FROM INFORMATION_SCHEMA.STATISTICS s
    WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
      AND s.SEQ_IN_INDEX = 1;

    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT(
                      'CREATE ',
                      CASE WHEN UPPER(i.IndexType) = 'SPATIAL' THEN 'SPATIAL '
                           WHEN i.IsUnique = 1 AND i.IsPrimaryKey = 0 THEN 'UNIQUE '
                           ELSE '' END,
                      'INDEX ', i.IndexName,
                      ' ON `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', i.TableName,
                      ' (', i.IndexColumns, ')',
                      CASE WHEN UPPER(i.IndexType) = 'HASH' THEN ' USING HASH'
                           WHEN UPPER(i.IndexType) = 'BTREE' THEN ' USING BTREE'
                           ELSE '' END,
                      CASE WHEN i.Comment IS NOT NULL AND i.Comment != ''
                           THEN CONCAT(' COMMENT ''', REPLACE(i.Comment, '''', ''''''), '''')
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
              SELECT 1 FROM _SchemaSmith_IdxExistPostDrop s
              WHERE s.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND s.IndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          -- A declared functional index below the floor (see the STEP 0.7 degrade guard above) is
          -- never created -- it is a hard syntax error, not a clause that can be suppressed.
          AND NOT (SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1 AND SchemaSmith_SupportsFunctionalIndex() = 0);

        -- #363: WhatIf twin of the ELSE-branch 'index'/'created' audit; same predicate, set-based wouldCreate.
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'index', CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName)), 'wouldCreate'
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND r.NewIndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IdxExistPostDrop s
              WHERE s.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND s.IndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT (SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1 AND SchemaSmith_SupportsFunctionalIndex() = 0);
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing indexes');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateIdxStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CreateIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- Detection reads the post-drop snapshot _SchemaSmith_IdxExistPostDrop (taken after STEP 2's
        -- drops) so a just-dropped modified index (STEP 2) is correctly seen as missing here and recreated.
        INSERT INTO _SchemaSmith_CreateIdxStmts (LogMsg, Stmt, AuditName)
        SELECT
            CONCAT('  Create index: ', SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName),
                CASE WHEN COALESCE(i.VariantName, '') <> '' THEN CONCAT(' (variant: ', i.VariantName, ')') ELSE '' END),
            CONCAT(
                'CREATE ',
                CASE WHEN UPPER(i.IndexType) = 'SPATIAL' THEN 'SPATIAL '
                     WHEN i.IsUnique = 1 AND i.IsPrimaryKey = 0 THEN 'UNIQUE '
                     ELSE '' END,
                'INDEX ', i.IndexName,
                ' ON `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', i.TableName,
                ' (', i.IndexColumns, ')',
                CASE WHEN UPPER(i.IndexType) = 'HASH' THEN ' USING HASH'
                     WHEN UPPER(i.IndexType) = 'BTREE' THEN ' USING BTREE'
                     ELSE '' END,
                CASE WHEN i.Comment IS NOT NULL AND i.Comment != ''
                     THEN CONCAT(' COMMENT ''', REPLACE(i.Comment, '''', ''''''), '''')
                     ELSE '' END,
                CASE WHEN i.IsVisible = 0 AND SchemaSmith_SupportsInvisibleIndex() = 1 THEN SchemaSmith_IndexInvisibleClause() ELSE '' END
            ),
            CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
        FROM _SchemaSmith_Indexes i
        WHERE i.IsPrimaryKey = 0
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND r.NewIndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IdxExistPostDrop s
              WHERE s.TableName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.TableName) COLLATE utf8mb4_unicode_ci
                AND s.IndexName COLLATE utf8mb4_unicode_ci = SchemaSmith_StripBacktickWrapping(i.IndexName) COLLATE utf8mb4_unicode_ci
          )
          AND NOT (SchemaSmith_IndexHasFunctionalKeyPart(i.IndexColumns) = 1 AND SchemaSmith_SupportsFunctionalIndex() = 0);

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateIdxStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_CreateIdxStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'index', @ss_auditname, 'created');
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateIdxStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateIdxStmts;
    END IF;

    -- =========================================================================
    -- STEP 3.5: Drop modified check constraints (table-level AND column-level)
    -- =========================================================================
    -- MySQL has no DDL to change a CHECK expression in place, so a constraint whose live
    -- expression no longer matches the desired one is dropped here and re-created by the
    -- create passes below (STEP 4 for table-level, STEP 4.5 for column-level). This closes
    -- the Bug 2 gap (a modified TABLE-level check used to drift silently) and gives parity
    -- with SQL Server / PostgreSQL, which both drop-then-recreate a changed check.
    --
    -- Comparison uses SchemaSmith_NormalizeCheckExpression on BOTH sides so an unchanged
    -- check is NOT phantom-dropped: MySQL reformats CHECK_CLAUSE on storage (adds an outer
    -- paren pair and normalizes spacing), which the normalizer collapses to a canonical form.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedChecks;
    CREATE TEMPORARY TABLE _SchemaSmith_ModifiedChecks (
        TableName VARCHAR(128) NOT NULL,
        ConstraintName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, ConstraintName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

    -- Table-level checks: live CHECK_CLAUSE differs from the desired _SchemaSmith_CheckConstraints.Expression.
    -- INFORMATION_SCHEMA.CHECK_CONSTRAINTS does not exist on MySQL 5.7 and MySQL binds INFORMATION_SCHEMA
    -- references at CREATE time, so both CHECK_CONSTRAINTS reads below live only inside dynamically-built
    -- strings, gated by SchemaSmith_SupportsCheckConstraints() (see GenerateTableJson for the full rationale).
    -- Below the floor there are no check constraints to detect as modified, so _SchemaSmith_ModifiedChecks
    -- simply stays unpopulated by this step.
    SET @v_mcDbName = p_DatabaseName;
    IF SchemaSmith_SupportsCheckConstraints() = 1 THEN
        SET @v_mcSql1 = 'INSERT IGNORE INTO _SchemaSmith_ModifiedChecks (TableName, ConstraintName)
SELECT
    SchemaSmith_StripBacktickWrapping(c.TableName) AS TableName,
    SchemaSmith_StripBacktickWrapping(c.ConstraintName) AS ConstraintName
FROM _SchemaSmith_CheckConstraints c
JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    ON BINARY tc.TABLE_SCHEMA = BINARY @v_mcDbName
    AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
    AND BINARY tc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
    AND tc.CONSTRAINT_TYPE = ''CHECK''
JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
    ON BINARY cc.CONSTRAINT_SCHEMA = BINARY @v_mcDbName
    AND BINARY cc.CONSTRAINT_NAME = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
WHERE BINARY SchemaSmith_NormalizeCheckExpression(CONVERT(cc.CHECK_CLAUSE USING utf8mb4))
    != BINARY SchemaSmith_NormalizeCheckExpression(c.Expression)';
        PREPARE stmt FROM @v_mcSql1;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;

    -- Column-level checks: keyed on the deterministic name CK_<table>_<column>; live CHECK_CLAUSE
    -- differs from the desired column CheckExpression. (INFORMATION_SCHEMA.CHECK_CONSTRAINTS has
    -- no column linkage, which is exactly why column checks carry a deterministic name.) Same
    -- CREATE-time binding constraint as above, so this read is also dynamic SQL under the same guard.
    IF SchemaSmith_SupportsCheckConstraints() = 1 THEN
        SET @v_mcSql2 = 'INSERT IGNORE INTO _SchemaSmith_ModifiedChecks (TableName, ConstraintName)
SELECT
    SchemaSmith_StripBacktickWrapping(col.TableName) AS TableName,
    CONCAT(''CK_'', SchemaSmith_StripBacktickWrapping(col.TableName), ''_'', SchemaSmith_StripBacktickWrapping(col.ColumnName)) AS ConstraintName
FROM _SchemaSmith_Columns col
JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    ON BINARY tc.TABLE_SCHEMA = BINARY @v_mcDbName
    AND BINARY tc.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(col.TableName)
    AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT(''CK_'', SchemaSmith_StripBacktickWrapping(col.TableName), ''_'', SchemaSmith_StripBacktickWrapping(col.ColumnName))
    AND tc.CONSTRAINT_TYPE = ''CHECK''
JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
    ON BINARY cc.CONSTRAINT_SCHEMA = BINARY @v_mcDbName
    AND BINARY cc.CONSTRAINT_NAME = BINARY CONCAT(''CK_'', SchemaSmith_StripBacktickWrapping(col.TableName), ''_'', SchemaSmith_StripBacktickWrapping(col.ColumnName))
WHERE col.CheckExpression IS NOT NULL
  AND TRIM(col.CheckExpression) != ''''
  AND BINARY SchemaSmith_NormalizeCheckExpression(CONVERT(cc.CHECK_CLAUSE USING utf8mb4))
    != BINARY SchemaSmith_NormalizeCheckExpression(col.CheckExpression)';
        PREPARE stmt FROM @v_mcSql2;
        EXECUTE stmt;
        DEALLOCATE PREPARE stmt;
    END IF;

    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop modified check constraints');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`')
        FROM _SchemaSmith_ModifiedChecks;
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop modified check constraints');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModChkStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_DropModChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_DropModChkStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Drop modified check constraint: ', TableName, '.', ConstraintName),
            CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`')
        FROM _SchemaSmith_ModifiedChecks;

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModChkStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_DropModChkStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropModChkStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropModChkStmts;
    END IF;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedChecks;

    -- =========================================================================
    -- No-drop protection tier (#270): capture check constraints that WOULD have been dropped by
    -- absence but are suppressed. Same by-absence predicate as the _SchemaSmith_ChecksToDropByAbsence
    -- build below, minus the env p_DropCheckConstraintsRemovedFromProduct gate (protection forces it
    -- false, so the drop pass is skipped) but keeping the per-table opt-out. Materialize the
    -- INFORMATION_SCHEMA read into a temp first (crash-safety), then a discrete audit insert. Audit
    -- rows only, so it runs regardless of p_WhatIf. The capture signal is the session user-variable
    -- @ss_capture_would_drop set by the caller on the connection (this proc takes no new parameter).
    -- ObjectName mirrors the drop pass's 'constraint'/'dropped' audit: CONCAT(TableName, '.', ConstraintName).
    IF COALESCE(@ss_capture_would_drop, 0) = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropChecks;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropChecks (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_WouldDropChecks (TableName, ConstraintName)
        SELECT CONVERT(tc.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
               CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        JOIN _SchemaSmith_Tables t
            ON BINARY SchemaSmith_StripBacktickWrapping(t.TableName) = BINARY tc.TABLE_NAME
            AND t.NewTable = 0
            AND COALESCE(t.DropCheckConstraintsRemovedFromProduct, 1) = 1
        WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
          AND tc.CONSTRAINT_TYPE = 'CHECK'
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_CheckConstraints c
              WHERE BINARY SchemaSmith_StripBacktickWrapping(c.TableName) = BINARY tc.TABLE_NAME
                AND BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName) = BINARY tc.CONSTRAINT_NAME)
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_Columns col
              WHERE BINARY SchemaSmith_StripBacktickWrapping(col.TableName) = BINARY tc.TABLE_NAME
                AND col.CheckExpression IS NOT NULL AND TRIM(col.CheckExpression) != ''
                AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(col.TableName), '_', SchemaSmith_StripBacktickWrapping(col.ColumnName)));

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'constraint', CONCAT(TableName, '.', ConstraintName), 'dropSuppressed'
        FROM _SchemaSmith_WouldDropChecks;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropChecks;
    END IF;

    -- =========================================================================
    -- Drop check constraints removed from the product (by-absence), gated by the cascade flag
    -- and per-table tightening. Scoped to the current quench's product tables. Table-level checks
    -- absent from _SchemaSmith_CheckConstraints are dropped; a column-level CK_<table>_<column>
    -- check is excluded only while its column still carries a CheckExpression (then it is owned by
    -- the modify/create passes); once the CheckExpression is removed, the orphan is cleaned up here.
    -- =========================================================================
    IF p_DropCheckConstraintsRemovedFromProduct = 1 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ChecksToDropByAbsence;
        CREATE TEMPORARY TABLE _SchemaSmith_ChecksToDropByAbsence (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        INSERT INTO _SchemaSmith_ChecksToDropByAbsence (TableName, ConstraintName)
        SELECT CONVERT(tc.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
               CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        JOIN _SchemaSmith_Tables t
            ON BINARY SchemaSmith_StripBacktickWrapping(t.TableName) = BINARY tc.TABLE_NAME
            AND t.NewTable = 0
            AND COALESCE(t.DropCheckConstraintsRemovedFromProduct, 1) = 1
        WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
          AND tc.CONSTRAINT_TYPE = 'CHECK'
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_CheckConstraints c
              WHERE BINARY SchemaSmith_StripBacktickWrapping(c.TableName) = BINARY tc.TABLE_NAME
                AND BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName) = BINARY tc.CONSTRAINT_NAME)
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_Columns col
              WHERE BINARY SchemaSmith_StripBacktickWrapping(col.TableName) = BINARY tc.TABLE_NAME
                AND col.CheckExpression IS NOT NULL AND TRIM(col.CheckExpression) != ''
                AND BINARY tc.CONSTRAINT_NAME = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(col.TableName), '_', SchemaSmith_StripBacktickWrapping(col.ColumnName)));

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop check constraints removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`')
            FROM _SchemaSmith_ChecksToDropByAbsence;

            -- #363: WhatIf twin of the ELSE-branch 'constraint'/'dropped' audit; same source, wouldDrop.
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'constraint', CONCAT(TableName, '.', ConstraintName), 'wouldDrop'
            FROM _SchemaSmith_ChecksToDropByAbsence;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop check constraints removed from product');

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropAbsChkStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_DropAbsChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT, AuditName TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_DropAbsChkStmts (Stmt, AuditName)
            SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ', SchemaSmith_DropCheckClause(), ' `', ConstraintName, '`'),
                   CONCAT(TableName, '.', ConstraintName)
            FROM _SchemaSmith_ChecksToDropByAbsence;

            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropAbsChkStmts);
            WHILE @ss_id IS NOT NULL DO
                SELECT Stmt, AuditName INTO @exec_sql, @ss_auditname FROM _SchemaSmith_DropAbsChkStmts WHERE RowId = @ss_id;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
                INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'constraint', @ss_auditname, 'dropped');
                DEALLOCATE PREPARE stmt;
                SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropAbsChkStmts WHERE RowId > @ss_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropAbsChkStmts;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ChecksToDropByAbsence;
    END IF;

    -- =========================================================================
    -- CHECK-constraint existence snapshot: taken HERE, after STEP 3.5's modified-check drops and the
    -- by-absence drops, so a just-dropped check is correctly seen as MISSING by the STEP 4 / STEP 4.5
    -- create passes and recreated -- the same reason STEP 3 snapshots after the index drops. In WhatIf
    -- mode nothing was dropped, so this reflects the unchanged catalog, matching the live reads it
    -- replaces. One row per CHECK constraint. STEP 7 (ownership) REBUILDS this same table post-create,
    -- so the create passes here see the pre-create state and the ownership pass sees the post-create state.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ChkExist;
    CREATE TEMPORARY TABLE _SchemaSmith_ChkExist (
        TableName VARCHAR(128) NOT NULL,
        ConstraintName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName, ConstraintName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    IF SchemaSmith_SupportsCheckConstraints() = 1 THEN
        INSERT INTO _SchemaSmith_ChkExist (TableName, ConstraintName)
        SELECT CONVERT(tc.TABLE_NAME USING utf8mb4), CONVERT(tc.CONSTRAINT_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
          AND tc.CONSTRAINT_TYPE = 'CHECK';
    END IF;

    -- =========================================================================
    -- STEP 4: Create missing check constraints (MySQL 8.0.16+)
    -- =========================================================================
    -- CHECK constraints require MySQL 8.0.16 (INFORMATION_SCHEMA.CHECK_CONSTRAINTS + enforcement); on MySQL
    -- 5.7 a declared CHECK is parsed-and-ignored, so it can neither be created nor detected as present -- which
    -- would make this create pass re-emit on every deploy. Degrade via the UnsupportedFeaturePolicy spine
    -- (mirrors the PostgreSQL NULLS-NOT-DISTINCT routing): 'fail' aborts naming the offending constraint(s);
    -- 'warn' (default) skips the emit and records one 'downgraded' manifest row per declared check so the run
    -- stays idempotent. MariaDB reports support at/above the 10.2 floor, so it never enters this branch.
    IF SchemaSmith_SupportsCheckConstraints() = 0 THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail'
           AND EXISTS (SELECT 1 FROM _SchemaSmith_CheckConstraints) THEN
            -- Log the full offending list to the run log first (SIGNAL MESSAGE_TEXT is capped at 128 chars,
            -- so the abort message stays concise + non-truncating and names the detail in the log instead).
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  CHECK constraint unsupported (requires MySQL 8.0.16): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName))
            FROM _SchemaSmith_CheckConstraints c;
            -- Keep < 128 chars: MySQL errors ("Data too long for condition item") on an over-long MESSAGE_TEXT.
            SET @ss_msg = CONCAT('CHECK constraints require MySQL 8.0.16 (detected ',
                                 SchemaSmith_ServerVersionNum(), '); see the deploy log for the unsupported check(s).');
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            -- Surface the downgrade in the run log too (not only the ChangeAudit manifest), matching this
            -- proc's per-object status-message convention, so a 5.7 deploy visibly reports skipped checks.
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Skipping check constraint (requires MySQL 8.0.16 - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName))
            FROM _SchemaSmith_CheckConstraints c;
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'CHECK constraint (MySQL 8.0.16)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName)), 'downgraded'
            FROM _SchemaSmith_CheckConstraints c;
        END IF;
    ELSEIF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing check constraints');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' ADD CONSTRAINT ', c.ConstraintName,
                      ' CHECK (', c.Expression, ')')
        FROM _SchemaSmith_CheckConstraints c
        WHERE NOT EXISTS (
            SELECT 1 FROM _SchemaSmith_ChkExist tc
            WHERE BINARY tc.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.ConstraintName = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
        );

        -- #363: WhatIf twin of the ELSE-branch 'constraint'/'created' (check) audit; same predicate, wouldCreate.
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'constraint', CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName)), 'wouldCreate'
        FROM _SchemaSmith_CheckConstraints c
        WHERE NOT EXISTS (
            SELECT 1 FROM _SchemaSmith_ChkExist tc
            WHERE BINARY tc.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.ConstraintName = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
        );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing check constraints');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateChkStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CreateChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- Detection reads the pre-create snapshot _SchemaSmith_ChkExist (taken after STEP 3.5's drops)
        -- so a just-dropped modified table check (STEP 3.5) is correctly seen as missing here and recreated.
        INSERT INTO _SchemaSmith_CreateChkStmts (LogMsg, Stmt, AuditName)
        SELECT
            CONCAT('  Create check constraint: ', c.TableName, '.', c.ConstraintName,
                CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END),
            CONCAT(
                'ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                ' ADD CONSTRAINT ', c.ConstraintName,
                ' CHECK (', c.Expression, ')'
            ),
            CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName))
        FROM _SchemaSmith_CheckConstraints c
        WHERE NOT EXISTS (
            SELECT 1 FROM _SchemaSmith_ChkExist tc
            WHERE BINARY tc.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.ConstraintName = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
        );

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateChkStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_CreateChkStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'constraint', @ss_auditname, 'created');
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateChkStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateChkStmts;
    END IF;

    -- =========================================================================
    -- STEP 4.5: Create missing column-level check constraints (MySQL 8.0.16+)
    -- =========================================================================
    -- A column's CheckExpression becomes a deterministically named CK_<table>_<column> check.
    -- The deterministic name lets the create/modify passes key on it (INFORMATION_SCHEMA has no
    -- column linkage for checks). Mirrors the table-level STEP 4 idiom exactly -- including the
    -- SchemaSmith_SupportsCheckConstraints() degrade for MySQL below 8.0.16 (see STEP 4).
    IF SchemaSmith_SupportsCheckConstraints() = 0 THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail'
           AND EXISTS (SELECT 1 FROM _SchemaSmith_Columns WHERE CheckExpression IS NOT NULL AND TRIM(CheckExpression) != '') THEN
            -- Log the full offending list first; keep the SIGNAL message concise (128-char cap). See STEP 4.
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Column CHECK constraint unsupported (requires MySQL 8.0.16): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.CK_',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            WHERE c.CheckExpression IS NOT NULL AND TRIM(c.CheckExpression) != '';
            -- Keep < 128 chars (see STEP 4).
            SET @ss_msg = CONCAT('CHECK constraints require MySQL 8.0.16 (detected ',
                                 SchemaSmith_ServerVersionNum(), '); see the deploy log for the unsupported column check(s).');
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            -- Surface the downgrade in the run log (see STEP 4).
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Skipping column check constraint (requires MySQL 8.0.16 - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.CK_',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            WHERE c.CheckExpression IS NOT NULL AND TRIM(c.CheckExpression) != '';
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'CHECK constraint (MySQL 8.0.16)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.CK_',
                          SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'downgraded'
            FROM _SchemaSmith_Columns c
            WHERE c.CheckExpression IS NOT NULL AND TRIM(c.CheckExpression) != '';
        END IF;
    ELSEIF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing column check constraints');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                      ' ADD CONSTRAINT `CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`',
                      ' CHECK (', c.CheckExpression, ')')
        FROM _SchemaSmith_Columns c
        WHERE c.CheckExpression IS NOT NULL
          AND TRIM(c.CheckExpression) != ''
          AND NOT EXISTS (
            SELECT 1 FROM _SchemaSmith_ChkExist tc
            WHERE BINARY tc.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.ConstraintName = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Create missing column check constraints');

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateColChkStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CreateColChkStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        -- Detection reads the pre-create snapshot _SchemaSmith_ChkExist (taken after STEP 3.5's drops)
        -- so a just-dropped modified column check (STEP 3.5) is correctly seen as missing here and recreated.
        INSERT INTO _SchemaSmith_CreateColChkStmts (LogMsg, Stmt)
        SELECT
            CONCAT('  Create column check constraint: ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.',
                   CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName)),
                CASE WHEN COALESCE(c.VariantName, '') <> '' THEN CONCAT(' (variant: ', c.VariantName, ')') ELSE '' END),
            CONCAT(
                'ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
                ' ADD CONSTRAINT `CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName), '`',
                ' CHECK (', c.CheckExpression, ')'
            )
        FROM _SchemaSmith_Columns c
        WHERE c.CheckExpression IS NOT NULL
          AND TRIM(c.CheckExpression) != ''
          AND NOT EXISTS (
            SELECT 1 FROM _SchemaSmith_ChkExist tc
            WHERE BINARY tc.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.ConstraintName = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        );

        SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateColChkStmts);
        WHILE @ss_id IS NOT NULL DO
            SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_CreateColChkStmts WHERE RowId = @ss_id;
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
            PREPARE stmt FROM @exec_sql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_CreateColChkStmts WHERE RowId > @ss_id);
        END WHILE;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CreateColChkStmts;
    END IF;

    -- =========================================================================
    -- STEP 7: Update ProductOwnership for managed objects
    -- Confirms objects created THIS run (STEP 3/4/4.5) via post-create existence snapshots taken here,
    -- so ownership is written only for what actually landed -- the same confirmation the original live
    -- INFORMATION_SCHEMA reads gave, now one scan each instead of one per declared object.
    -- =========================================================================
    IF p_WhatIf = 0 THEN
        -- Post-create existence snapshots (indexes + CHECK constraints), reflecting STEP 3/4/4.5 creates.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxExistFinal;
        CREATE TEMPORARY TABLE _SchemaSmith_IdxExistFinal (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IdxExistFinal (TableName, IndexName)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.SEQ_IN_INDEX = 1;

        -- Rebuild the CHECK existence snapshot to the post-create state (the create passes above saw the
        -- pre-create build; ownership must see what now exists).
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ChkExist;
        CREATE TEMPORARY TABLE _SchemaSmith_ChkExist (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        IF SchemaSmith_SupportsCheckConstraints() = 1 THEN
            INSERT INTO _SchemaSmith_ChkExist (TableName, ConstraintName)
            SELECT CONVERT(tc.TABLE_NAME USING utf8mb4), CONVERT(tc.CONSTRAINT_NAME USING utf8mb4)
            FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
            WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
              AND tc.CONSTRAINT_TYPE = 'CHECK';
        END IF;

        -- Track indexes
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'INDEX',
               CONCAT(SchemaSmith_StripBacktickWrapping(i.TableName), '.', SchemaSmith_StripBacktickWrapping(i.IndexName))
        FROM _SchemaSmith_Indexes i
        WHERE EXISTS (
            SELECT 1 FROM _SchemaSmith_IdxExistFinal s
            WHERE BINARY s.TableName = BINARY SchemaSmith_StripBacktickWrapping(i.TableName)
              AND BINARY s.IndexName = BINARY SchemaSmith_StripBacktickWrapping(i.IndexName)
        );

        -- Track check constraints
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'CHECK CONSTRAINT',
               CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ConstraintName))
        FROM _SchemaSmith_CheckConstraints c
        WHERE EXISTS (
            SELECT 1 FROM _SchemaSmith_ChkExist tc
            WHERE BINARY tc.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.ConstraintName = BINARY SchemaSmith_StripBacktickWrapping(c.ConstraintName)
        );

        -- Track column-level check constraints (deterministic CK_<table>_<column> name)
        INSERT IGNORE INTO SchemaSmith_ProductOwnership (ProductName, TemplateName, ObjectSchema, ObjectType, ObjectName)
        SELECT p_ProductName, '', p_DatabaseName, 'CHECK CONSTRAINT',
               CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.CK_',
                      SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        FROM _SchemaSmith_Columns c
        WHERE c.CheckExpression IS NOT NULL
          AND TRIM(c.CheckExpression) != ''
          AND EXISTS (
            SELECT 1 FROM _SchemaSmith_ChkExist tc
            WHERE BINARY tc.TableName = BINARY SchemaSmith_StripBacktickWrapping(c.TableName)
              AND BINARY tc.ConstraintName = BINARY CONCAT('CK_', SchemaSmith_StripBacktickWrapping(c.TableName), '_', SchemaSmith_StripBacktickWrapping(c.ColumnName))
        );
    END IF;

    -- =========================================================================
    -- No-drop protection tier (#270): capture indexes that WOULD have been dropped by absence but
    -- are suppressed. STEP 8 below is skipped entirely under protection (the caller forces both
    -- p_DropUnknownIndexes and p_DropIndexesRemovedFromProduct false), so its _SchemaSmith_Step8Idx
    -- snapshot is never built; this block is self-contained — it snapshots the catalog
    -- (INFORMATION_SCHEMA out of the set-based DML, #337 crash-safety), then computes BOTH STEP 8
    -- axes' by-absence candidates MINUS their env gates into one temp — AXIS 1 removed-from-product
    -- (keeps the per-table COALESCE(t.DropIndexesRemovedFromProduct,1) opt-out, PRIMARY/rename/
    -- owned-by-product exclusions) UNION AXIS 2 unknown/out-of-band — then a discrete audit insert.
    -- Modified/for-change indexes are NOT captured: they remain in _SchemaSmith_Indexes so both axes'
    -- "not in current definition" predicate excludes them (STEP 2 drops-then-recreates them, a
    -- transient change, never a protection-withheld drop). Audit rows only, so it runs regardless of
    -- p_WhatIf. The capture signal is the session user-variable @ss_capture_would_drop set by the
    -- caller on the connection (this proc takes no new parameter). ObjectName/ObjectType mirror STEP
    -- 8's 'index'/'dropped' audit: ObjectType 'index', ObjectName CONCAT(TableName, '.', IndexName).
    IF COALESCE(@ss_capture_would_drop, 0) = 1 THEN
        -- Catalog snapshot (one row per index; SEQ_IN_INDEX = 1). Mirrors STEP 8's _SchemaSmith_Step8Idx.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8IdxCat;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropStep8IdxCat (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_WouldDropStep8IdxCat (TableName, IndexName)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.SEQ_IN_INDEX = 1;

        -- Defined-index snapshot (table.index pairs from the current definition). This is the
        -- "not in current definition" bridge and is exactly what keeps modified indexes (still in
        -- _SchemaSmith_Indexes) out of the capture set.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8DefIdx;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropStep8DefIdx (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_WouldDropStep8DefIdx (TableName, IndexName)
        SELECT CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4),
               CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4)
        FROM _SchemaSmith_Indexes i;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8Indexes;
        CREATE TEMPORARY TABLE _SchemaSmith_WouldDropStep8Indexes (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- AXIS 1 — removed-from-product (env gate p_DropIndexesRemovedFromProduct dropped; per-table opt-out kept).
        INSERT IGNORE INTO _SchemaSmith_WouldDropStep8Indexes (TableName, IndexName)
        SELECT DISTINCT
            SUBSTRING_INDEX(po.ObjectName, '.', 1),
            SUBSTRING_INDEX(po.ObjectName, '.', -1)
        FROM SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
        LEFT JOIN _SchemaSmith_WouldDropStep8DefIdx di
            ON CONVERT(di.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        LEFT JOIN _SchemaSmith_WouldDropStep8IdxCat ei
            ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = CONVERT(p_ProductName USING utf8mb4) COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
          -- Per-table tightening (the env/product gate is removed for capture)
          AND COALESCE(t.DropIndexesRemovedFromProduct, 1) = 1
          -- Never drop PRIMARY KEY
          AND UPPER(SUBSTRING_INDEX(po.ObjectName, '.', -1) COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition (LEFT JOIN produces NULL when not found) — also excludes modified indexes
          AND di.IndexName IS NULL
          -- Not a renamed index
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE r.TableName = SUBSTRING_INDEX(po.ObjectName, '.', 1)
                AND r.OldIndexName = SUBSTRING_INDEX(po.ObjectName, '.', -1)
          )
          -- Verify index actually exists (snapshot bridge)
          AND ei.IndexName IS NOT NULL;

        -- AXIS 2 — out-of-band (env gate p_DropUnknownIndexes dropped).
        INSERT IGNORE INTO _SchemaSmith_WouldDropStep8Indexes (TableName, IndexName)
        SELECT CONVERT(ei.TableName USING utf8mb4), CONVERT(ei.IndexName USING utf8mb4)
        FROM _SchemaSmith_WouldDropStep8IdxCat ei
        INNER JOIN _SchemaSmith_Tables t
            ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
        WHERE UPPER(ei.IndexName COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
          -- Not in current definition — also excludes modified indexes (still present in _SchemaSmith_Indexes)
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_WouldDropStep8DefIdx di
              WHERE CONVERT(di.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                AND CONVERT(di.IndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
          )
          -- Not owned by this product
          AND NOT EXISTS (
              SELECT 1 FROM SchemaSmith_ProductOwnership po
              WHERE po.ProductName COLLATE utf8mb4_unicode_ci = CONVERT(p_ProductName USING utf8mb4) COLLATE utf8mb4_unicode_ci
                AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci
                AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
                AND po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(ei.TableName, '.', ei.IndexName) COLLATE utf8mb4_unicode_ci
          )
          -- Not a renamed index
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_IndexRenames r
              WHERE CONVERT(r.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                AND CONVERT(r.OldIndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
          );

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'index', CONCAT(TableName, '.', IndexName), 'dropSuppressed'
        FROM _SchemaSmith_WouldDropStep8Indexes;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8Indexes;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8DefIdx;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropStep8IdxCat;
    END IF;

    -- =========================================================================
    -- STEP 8: Drop indexes not in the definition — two disjoint axes, matching SQL Server / PostgreSQL:
    --   * removed-from-product (owned by this product, no longer in the JSON) — gated by
    --     p_DropIndexesRemovedFromProduct (default on) + per-table tightening.
    --   * out-of-band (in the catalog, NOT owned by this product) — gated by p_DropUnknownIndexes
    --     (default off).
    -- =========================================================================
    IF p_DropUnknownIndexes = 1 OR p_DropIndexesRemovedFromProduct = 1 THEN
        -- Crash-safe snapshot for STEP 8 ONLY. Taken HERE (after STEP 0..7's creates/drops/renames)
        -- so it reflects the current catalog state, exactly what the original live reads saw at this
        -- point. STEP 8's ProductOwnership x STATISTICS detection is the frequent-run segfault trigger
        -- the roadmap identifies, so it (and its FK-drop join) read these snapshots instead of live
        -- INFORMATION_SCHEMA. SEQ_IN_INDEX = 1 yields one row per index (NON_UNIQUE is index-level),
        -- collapsing the original LEFT JOIN s + EXISTS s2 into a single snapshot reference (no 1137).
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8Idx;
        CREATE TEMPORARY TABLE _SchemaSmith_Step8Idx (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            NonUnique TINYINT DEFAULT 0,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_Step8Idx (TableName, IndexName, NonUnique)
        SELECT CONVERT(s.TABLE_NAME USING utf8mb4), CONVERT(s.INDEX_NAME USING utf8mb4), s.NON_UNIQUE
        FROM INFORMATION_SCHEMA.STATISTICS s
        WHERE BINARY s.TABLE_SCHEMA = BINARY p_DatabaseName
          AND s.SEQ_IN_INDEX = 1;

        -- FK rows referencing a product table (for the FK-before-index drop join). Same-schema FKs.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8KCU;
        CREATE TEMPORARY TABLE _SchemaSmith_Step8KCU (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            ReferencedTableName VARCHAR(128) DEFAULT NULL,
            KEY ix_s8kcu (ReferencedTableName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_Step8KCU (TableName, ConstraintName, ReferencedTableName)
        SELECT CONVERT(kcu.TABLE_NAME USING utf8mb4), CONVERT(kcu.CONSTRAINT_NAME USING utf8mb4), CONVERT(kcu.REFERENCED_TABLE_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
        WHERE BINARY kcu.REFERENCED_TABLE_SCHEMA = BINARY p_DatabaseName
          AND BINARY kcu.TABLE_SCHEMA = BINARY p_DatabaseName
          AND kcu.REFERENCED_TABLE_NAME IS NOT NULL;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8TC;
        CREATE TEMPORARY TABLE _SchemaSmith_Step8TC (
            TableName VARCHAR(128) NOT NULL,
            ConstraintName VARCHAR(128) NOT NULL,
            KEY ix_s8tc (TableName, ConstraintName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_Step8TC (TableName, ConstraintName)
        SELECT CONVERT(tc.TABLE_NAME USING utf8mb4), CONVERT(tc.CONSTRAINT_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
        WHERE BINARY tc.TABLE_SCHEMA = BINARY p_DatabaseName
          AND tc.CONSTRAINT_TYPE = 'FOREIGN KEY';

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
        CREATE TEMPORARY TABLE _SchemaSmith_IndexesToDrop (
            TableName VARCHAR(128) NOT NULL,
            IndexName VARCHAR(128) NOT NULL,
            IsUnique TINYINT DEFAULT 0,
            PRIMARY KEY (TableName, IndexName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- AXIS 1 — removed-from-product: indexes OWNED by this product but no longer in the
        -- definition. Gated by p_DropIndexesRemovedFromProduct (env/product) + per-table tightening.
        -- IMPORTANT: Only consider indexes on tables that ARE in the current definition (_SchemaSmith_Tables)
        -- so an index on a table not in the current JSON is never dropped.
        -- Reads _SchemaSmith_Step8Idx (single LEFT JOIN): existence == ei.IndexName IS NOT NULL,
        -- IsUnique == (ei.NonUnique = 0) -- collapses the original STATISTICS s + s2 references.
        IF p_DropIndexesRemovedFromProduct = 1 THEN
            INSERT INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
            SELECT
                SUBSTRING_INDEX(po.ObjectName, '.', 1) AS TableName,
                SUBSTRING_INDEX(po.ObjectName, '.', -1) AS IndexName,
                COALESCE(ei.NonUnique = 0, 0) AS IsUnique
            FROM SchemaSmith_ProductOwnership po
            -- Join with _SchemaSmith_Tables to only consider indexes on tables in the current definition
            INNER JOIN _SchemaSmith_Tables t
                ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
            LEFT JOIN _SchemaSmith_Step8Idx ei
                ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            LEFT JOIN _SchemaSmith_Indexes i
                ON CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', 1) USING utf8mb4)
                AND CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4) = CONVERT(SUBSTRING_INDEX(po.ObjectName, '.', -1) USING utf8mb4)
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = CONVERT(p_ProductName USING utf8mb4) COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
              -- Per-table tightening (the env/product gate is the enclosing IF)
              AND COALESCE(t.DropIndexesRemovedFromProduct, 1) = 1
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
              AND ei.IndexName IS NOT NULL;
        END IF;

        -- AXIS 2 — out-of-band: an index present in the catalog on a current-quench table that is
        -- NOT in the definition AND NOT owned by this product (e.g. hand-created via DDL). Gated by
        -- p_DropUnknownIndexes. Crash-safe: reads the Step8Idx snapshot + temp/real tables only —
        -- never INFORMATION_SCHEMA in this set-based DML (#337). INSERT IGNORE dedupes against any
        -- removed-from-product row already queued (shared PK on _SchemaSmith_IndexesToDrop).
        IF p_DropUnknownIndexes = 1 THEN
            INSERT IGNORE INTO _SchemaSmith_IndexesToDrop (TableName, IndexName, IsUnique)
            SELECT CONVERT(ei.TableName USING utf8mb4), CONVERT(ei.IndexName USING utf8mb4), COALESCE(ei.NonUnique = 0, 0)
            FROM _SchemaSmith_Step8Idx ei
            INNER JOIN _SchemaSmith_Tables t
                ON CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
            WHERE UPPER(ei.IndexName COLLATE utf8mb4_unicode_ci) != _utf8mb4'PRIMARY' COLLATE utf8mb4_unicode_ci
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Indexes i
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(i.TableName) USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                    AND CONVERT(SchemaSmith_StripBacktickWrapping(i.IndexName) USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM SchemaSmith_ProductOwnership po
                  WHERE po.ProductName COLLATE utf8mb4_unicode_ci = CONVERT(p_ProductName USING utf8mb4) COLLATE utf8mb4_unicode_ci
                    AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci
                    AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci
                    AND po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(ei.TableName, '.', ei.IndexName) COLLATE utf8mb4_unicode_ci
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_IndexRenames r
                  WHERE CONVERT(r.TableName USING utf8mb4) = CONVERT(ei.TableName USING utf8mb4)
                    AND CONVERT(r.OldIndexName USING utf8mb4) = CONVERT(ei.IndexName USING utf8mb4)
              );
        END IF;

        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('DROP INDEX `', IndexName, '` ON `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
            FROM _SchemaSmith_IndexesToDrop;

            -- #363: WhatIf twin of the ELSE-branch 'index'/'dropped' audit; same source + AuditName form.
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'index', CONCAT(TableName, '.', IndexName), 'wouldDrop'
            FROM _SchemaSmith_IndexesToDrop;
        ELSE
            -- First, drop any foreign keys that reference unique indexes we're about to drop.
            -- FK drops MUST complete before the index drops below (a FK backed by a unique index
            -- blocks dropping that index); these are two sequential loops, preserving that order.
            -- Reads the STEP 8 snapshots (Step8Idx existence bridge, Step8KCU, Step8TC).
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropFKForIdxStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_DropFKForIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            -- DISTINCT: one DROP FOREIGN KEY per FK. A composite FK yields multiple KEY_COLUMN_USAGE
            -- rows (one per column), and several dropped indexes on the same referenced table re-join
            -- the same FK; both would otherwise emit a duplicate DROP FOREIGN KEY (error 1091 on the
            -- second). The original per-row cursor had the same latent duplication.
            INSERT INTO _SchemaSmith_DropFKForIdxStmts (LogMsg, Stmt)
            SELECT DISTINCT
                CONCAT('  Drop FK for index: ',
                       CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', kcu.TableName, '` DROP FOREIGN KEY `', tc.ConstraintName, '`')),
                CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', kcu.TableName, '` DROP FOREIGN KEY `', tc.ConstraintName, '`')
            FROM _SchemaSmith_IndexesToDrop itd
            JOIN _SchemaSmith_Step8Idx ei
                ON CONVERT(ei.TableName USING utf8mb4) = CONVERT(itd.TableName USING utf8mb4)
                AND CONVERT(ei.IndexName USING utf8mb4) = CONVERT(itd.IndexName USING utf8mb4)
            JOIN _SchemaSmith_Step8KCU kcu
                ON CONVERT(kcu.ReferencedTableName USING utf8mb4) = CONVERT(itd.TableName USING utf8mb4)
            JOIN _SchemaSmith_Step8TC tc
                ON CONVERT(tc.TableName USING utf8mb4) = CONVERT(kcu.TableName USING utf8mb4)
                AND CONVERT(tc.ConstraintName USING utf8mb4) = CONVERT(kcu.ConstraintName USING utf8mb4)
            WHERE itd.IsUnique = 1;

            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropFKForIdxStmts);
            WHILE @ss_id IS NOT NULL DO
                SELECT LogMsg, Stmt INTO @ss_log, @exec_sql FROM _SchemaSmith_DropFKForIdxStmts WHERE RowId = @ss_id;
                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
                SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropFKForIdxStmts WHERE RowId > @ss_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropFKForIdxStmts;

            -- Now drop the unknown indexes
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unknown indexes');

            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropIdxStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_DropIdxStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, LogMsg TEXT, Stmt TEXT, AuditName TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_DropIdxStmts (LogMsg, Stmt, AuditName)
            SELECT
                CONCAT('  Drop unknown index: ', TableName, '.', IndexName),
                CONCAT('DROP INDEX `', IndexName, '` ON `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`'),
                CONCAT(TableName, '.', IndexName)
            FROM _SchemaSmith_IndexesToDrop;

            SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropIdxStmts);
            WHILE @ss_id IS NOT NULL DO
                SELECT LogMsg, Stmt, AuditName INTO @ss_log, @exec_sql, @ss_auditname FROM _SchemaSmith_DropIdxStmts WHERE RowId = @ss_id;
                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), @ss_log);
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                -- Object-change audit (#243 E5): after EXECUTE, before DEALLOCATE (crash-safe #337 point).
                INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (CONNECTION_ID(), 'index', @ss_auditname, 'dropped');
                DEALLOCATE PREPARE stmt;
                SET @ss_id := (SELECT MIN(RowId) FROM _SchemaSmith_DropIdxStmts WHERE RowId > @ss_id);
            END WHILE;
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_DropIdxStmts;

            -- Remove dropped indexes from ProductOwnership
            DELETE po FROM SchemaSmith_ProductOwnership po
            INNER JOIN _SchemaSmith_IndexesToDrop itd
                ON po.ObjectName COLLATE utf8mb4_unicode_ci = CONCAT(itd.TableName, '.', itd.IndexName) COLLATE utf8mb4_unicode_ci
            WHERE po.ProductName COLLATE utf8mb4_unicode_ci = CONVERT(p_ProductName USING utf8mb4) COLLATE utf8mb4_unicode_ci
              AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci
              AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci;
        END IF;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexesToDrop;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8Idx;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8KCU;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_Step8TC;
    END IF;

    -- Cleanup temporary tables
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IndexRenames;
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifiedIndexes;

END//

DELIMITER ;
