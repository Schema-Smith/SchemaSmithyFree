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
    -- 5.5. Comment changes (table-level)
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

    -- =========================================================================
    -- Degrade column DEFAULT expressions below MySQL 8.0.13 for EXISTING columns (new columns are
    -- gated in MissingTableAndColumnQuench, which runs earlier in the same deploy). MariaDB has
    -- supported expression defaults since 10.2.1 (MDEV-10134), at/below our 10.2 floor, so this branch
    -- is MySQL-only. Below the threshold an existing plain-default column cannot legally acquire an
    -- expression default -- a hard syntax error, not parsed-and-ignored -- so there is no safe partial
    -- MODIFY: 'fail' aborts naming the column(s); 'warn' (default) skips the column's modification
    -- entirely -- STEP 3 below excludes it via the identical predicate, leaving its current default in
    -- place -- and records a 'downgraded' manifest row per column.
    -- =========================================================================
    IF SchemaSmith_SupportsDefaultExpression() = 0
       AND EXISTS (SELECT 1 FROM _SchemaSmith_Columns c
                   INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                   WHERE t.NewTable = 0 AND c.NewColumn = 0
                     AND c.IsAutoIncrement = 0
                     AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
                     AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%') THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail' THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Column DEFAULT expression unsupported (requires MySQL 8.0.13): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.IsAutoIncrement = 0
              AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
              AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%';
            SET @ss_msg = CONCAT('Column DEFAULT expressions require MySQL 8.0.13 (detected ',
                                 SchemaSmith_ServerVersionNum(), '); see the deploy log for the unsupported column(s).');
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Skipping column modification (DEFAULT expression requires MySQL 8.0.13 - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.IsAutoIncrement = 0
              AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
              AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%';
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'column (DEFAULT expression, MySQL 8.0.13)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'downgraded'
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.IsAutoIncrement = 0
              AND (c.GeneratedExpression IS NULL OR TRIM(c.GeneratedExpression) = '')
              AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%';
        END IF;
    END IF;

    -- =========================================================================
    -- Degrade invisible columns below MySQL 8.0.23 / MariaDB 10.3 for EXISTING columns (new columns are
    -- gated in MissingTableAndColumnQuench, which runs earlier in the same deploy). Below the threshold
    -- the INVISIBLE keyword is a hard syntax error, so ColumnScript never emits it there -- the MODIFY
    -- COLUMN emitted below (STEP 3) is still syntactically valid, it just leaves the column visible -- and
    -- STEP 3's predicate ignores the visibility difference so the deploy stays idempotent. This block only
    -- adds the user-facing report: 'fail' aborts naming the column(s); 'warn' (default) records a
    -- 'downgraded' manifest row per column.
    -- =========================================================================
    IF SchemaSmith_SupportsInvisibleColumn() = 0
       AND EXISTS (SELECT 1 FROM _SchemaSmith_Columns c
                   INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                   WHERE t.NewTable = 0 AND c.NewColumn = 0
                     AND c.IsInvisible = 1) THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail' THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Invisible column requires MySQL 8.0.23 / MariaDB 10.3 (UnsupportedFeaturePolicy=fail): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.IsInvisible = 1;
            SET @ss_msg = 'Invisible column requires MySQL 8.0.23 / MariaDB 10.3 (UnsupportedFeaturePolicy=fail). See the run log for the full list.';
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Invisible column stored visible (requires MySQL 8.0.23 / MariaDB 10.3 - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.IsInvisible = 1;
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'column (invisible, MySQL 8.0.23 / MariaDB 10.3)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'downgraded'
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.IsInvisible = 1;
        END IF;
    END IF;

    -- =========================================================================
    -- Degrade column SRID restriction below MySQL 8.0.3 for EXISTING columns (new columns are gated in
    -- MissingTableAndColumnQuench, which runs earlier in the same deploy). MariaDB has no equivalent
    -- attribute at any version, so SchemaSmith_SupportsColumnSrid() is 0 there unconditionally -- this
    -- block fires for MariaDB the same way it fires for a genuinely old MySQL. Below the threshold the
    -- SRID keyword is a hard syntax error, so ColumnScript never emits it there -- the MODIFY COLUMN
    -- emitted below (STEP 3) is still syntactically valid, it just leaves the column unrestricted -- and
    -- STEP 3's predicate ignores the SRID difference so the deploy stays idempotent. This block only
    -- adds the user-facing report: 'fail' aborts naming the column(s); 'warn' (default) records a
    -- 'downgraded' manifest row per column.
    -- =========================================================================
    IF SchemaSmith_SupportsColumnSrid() = 0
       AND EXISTS (SELECT 1 FROM _SchemaSmith_Columns c
                   INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
                   WHERE t.NewTable = 0 AND c.NewColumn = 0
                     AND c.Srid IS NOT NULL) THEN
        IF SchemaSmith_UnsupportedFeaturePolicy() = 'fail' THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Column SRID requires MySQL 8.0.3 (MariaDB unsupported) (UnsupportedFeaturePolicy=fail): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.Srid IS NOT NULL;
            SET @ss_msg = 'Column SRID requires MySQL 8.0.3 (MariaDB unsupported) (UnsupportedFeaturePolicy=fail). See the run log for the full list.';
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        ELSE
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Column SRID stored unrestricted (requires MySQL 8.0.3, MariaDB unsupported - downgraded): ',
                   SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName))
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.Srid IS NOT NULL;
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'column (SRID, MySQL 8.0.3)',
                   CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'downgraded'
            FROM _SchemaSmith_Columns c
            INNER JOIN _SchemaSmith_Tables t ON t.TableName = c.TableName
            WHERE t.NewTable = 0 AND c.NewColumn = 0
              AND c.Srid IS NOT NULL;
        END IF;
    END IF;

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

    -- Existing-table snapshot: the "does table X exist" checks below (ownership reconcile, ownership
    -- write, the STEP 8 drop-candidate builds, the PreventDrop report) each read INFORMATION_SCHEMA.TABLES
    -- per owned/declared table. INFORMATION_SCHEMA is not a stored table on MySQL/MariaDB, so those per-row
    -- reads re-materialise the server-wide table list every time (cost = tables-processed x tables-on-instance).
    -- Snapshot the schema's table names ONCE here and check against it. Table existence is stable from proc
    -- start through STEP 7 (renames already ran upstream in MissingTableAndColumnQuench; STEPs 3-7 only ALTER,
    -- never create/drop), so this pre-STEP-8 snapshot is what every check before STEP 8 reads. STEP 9's prune
    -- needs the POST-drop state, so the snapshot is REBUILT there. In WhatIf nothing is dropped, so it stays
    -- accurate. _SchemaSmith_ExistingTablesN is a copy: STEP 1's rename reconcile references the snapshot twice
    -- in one statement (new-name EXISTS + old-name NOT EXISTS), which MySQL/MariaDB forbid for a TEMPORARY
    -- table (ER_CANT_REOPEN_TABLE 1137); the second reference reads the copy.
    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ExistingTables;
    CREATE TEMPORARY TABLE _SchemaSmith_ExistingTables (
        TableName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    -- No TABLE_TYPE filter: the original per-row checks read INFORMATION_SCHEMA.TABLES unfiltered (which
    -- includes views), so the snapshot must too, to stay behaviour-identical.
    INSERT INTO _SchemaSmith_ExistingTables (TableName)
    SELECT CONVERT(ist.TABLE_NAME USING utf8mb4)
    FROM INFORMATION_SCHEMA.TABLES ist
    WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName;

    DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ExistingTablesN;
    CREATE TEMPORARY TABLE _SchemaSmith_ExistingTablesN (
        TableName VARCHAR(128) NOT NULL,
        PRIMARY KEY (TableName)
    ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
    INSERT INTO _SchemaSmith_ExistingTablesN (TableName) SELECT TableName FROM _SchemaSmith_ExistingTables;

    -- =======================
    -- STEP 1: RECONCILE OWNERSHIP FOR RENAMED TABLES
    -- =======================
    -- The physical table + column renames now run in MissingTableAndColumnQuench, BEFORE the
    -- add-columns step, so a carried or newly-added column targets the post-rename table name
    -- (parity with SQL Server / PostgreSQL, which rename before adding columns). Only the
    -- ProductOwnership reconciliation stays here, where the product name is in scope: rewrite the
    -- owner row old->new for tables whose rename has taken effect (new name now present in the
    -- catalog, old name gone). Read-only in WhatIf (nothing was renamed).
    -- Note: BINARY comparisons avoid collation clashes between INFORMATION_SCHEMA (utf8mb3),
    -- SchemaSmith functions (utf8mb4_unicode_ci), and connection params (utf8mb4_0900_ai_ci).
    IF p_WhatIf = 0 THEN
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenames;
        CREATE TEMPORARY TABLE _SchemaSmith_TableRenames (
            OldTableName VARCHAR(128) NOT NULL,
            NewTableName VARCHAR(128) NOT NULL,
            PRIMARY KEY (OldTableName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

        -- Post-rename predicate: the new name now exists and the old name is gone (the rename in
        -- MissingTableAndColumnQuench succeeded), so the owner row still keyed on the old name needs
        -- to move to the new name.
        INSERT INTO _SchemaSmith_TableRenames (OldTableName, NewTableName)
        SELECT
            SchemaSmith_StripBacktickWrapping(t.OldName),
            SchemaSmith_StripBacktickWrapping(t.TableName)
        FROM _SchemaSmith_Tables t
        WHERE t.OldName IS NOT NULL
          AND t.NewTable = 0
          AND EXISTS (
              SELECT 1 FROM _SchemaSmith_ExistingTables ist
              WHERE BINARY ist.TableName = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_ExistingTablesN ist
              WHERE BINARY ist.TableName = BINARY SchemaSmith_StripBacktickWrapping(t.OldName)
          );

        -- Update ProductOwnership for the renamed tables (set-based join, one UPDATE for all pairs).
        UPDATE SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_TableRenames r
            ON po.ObjectName COLLATE utf8mb4_unicode_ci = r.OldTableName COLLATE utf8mb4_unicode_ci
        SET po.ObjectName = r.NewTableName
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = CONVERT(p_ProductName USING utf8mb4) COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'TABLE' COLLATE utf8mb4_unicode_ci;

        -- OldName constraint-rename parity: INDEX ownership rows are keyed as 'tablename.indexname'
        -- (see MissingIndexesAndConstraintsQuench). The TABLE update above only moved TABLE rows, so an
        -- index ownership row still carries the OLD table prefix after the rename. The downstream index
        -- rename/drop passes key on the NEW table name, so they would miss the carried-over index and
        -- leave the old-named index behind as a silent duplicate. Rewrite the table prefix old->new for
        -- INDEX rows on renamed tables so those passes recognise and rename (or drop) it.
        UPDATE SchemaSmith_ProductOwnership po
        INNER JOIN _SchemaSmith_TableRenames r
            ON po.ObjectName COLLATE utf8mb4_unicode_ci LIKE CONCAT(r.OldTableName, '.%') COLLATE utf8mb4_unicode_ci
        SET po.ObjectName = CONCAT(r.NewTableName, SUBSTRING(po.ObjectName FROM CHAR_LENGTH(r.OldTableName) + 1))
        WHERE po.ProductName COLLATE utf8mb4_unicode_ci = CONVERT(p_ProductName USING utf8mb4) COLLATE utf8mb4_unicode_ci
          AND po.ObjectSchema COLLATE utf8mb4_unicode_ci = CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci
          AND po.ObjectType COLLATE utf8mb4_unicode_ci = _utf8mb4'INDEX' COLLATE utf8mb4_unicode_ci;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_TableRenames;
    END IF;

    -- =======================
    -- STEP 2: COLUMN RENAMES — moved to MissingTableAndColumnQuench
    -- =======================
    -- Declarative column renames now run there (before add-columns), alongside the table rename, so
    -- a renamed column is in place before the add-columns pass and a table+column rename in one
    -- deploy works. Nothing to do here.

    -- =======================
    -- STEP 3: COLUMN MODIFICATIONS (excludes generated status changes)
    -- =======================
    -- Output column modifications
    -- NOTE: Excludes columns that are changing their generated status since MySQL
    -- doesn't support that via ALTER MODIFY - those are handled in Step 3.5
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Modify columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
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
          -- A desired expression-form default this target cannot legally MODIFY into (below MySQL
          -- 8.0.13 / see the degrade guard above) drops the whole column out of this pass; its
          -- current default is left in place rather than emitting a MODIFY that would hit a syntax error.
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
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
                   ELSE SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC'))
                     != SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')) END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              -- Default value changes (strip outer single quotes from JSON default for comparison,
              -- since GenerateTableJson wraps string/enum defaults in quotes for DDL but INFORMATION_SCHEMA stores raw values)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NULL OR BINARY SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END)
                  -- A DECIMAL default comes back at the column's scale ('0' declared, '0.00' stored), which
                  -- never matched as text and re-ALTERed the column on every deploy.
                  AND SchemaSmith_NumericDefaultsEqual(SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT),
                                                      c.DefaultValue, isc.DATA_TYPE) = 0)
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NOT NULL)
              -- Collation changes (only when JSON specifies a collation)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              -- Generated expression changes (both sides are generated, but expression differs)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              -- AUTO_INCREMENT removal/addition (live EXTRA vs declared IsAutoIncrement) — parity with identity removal
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
              -- Invisible-column visibility differs. Gated behind SchemaSmith_SupportsInvisibleColumn(): below
              -- the floor (MySQL 8.0.23 / MariaDB 10.3) the column can never actually become invisible (the
              -- degrade guard above already reports it), so ignore the difference there or it churns every run.
              -- Symmetric in both directions -- a declared column newly marked invisible (visible -> invisible)
              -- and one whose Invisible flag was removed (invisible -> visible) both trip this <> compare.
              OR (SchemaSmith_SupportsInvisibleColumn() = 1 AND (isc.EXTRA LIKE '%INVISIBLE%') <> (c.IsInvisible = 1))
              -- Column SRID differs. Gated behind SchemaSmith_SupportsColumnSrid() (MySQL 8.0.3+; MariaDB
              -- never -- the degrade guard above already reports it there), so ignore the difference below
              -- the floor or it churns every run. isc.SRS_ID cannot be read directly here (absent on
              -- MariaDB -- see SchemaSmith_ColumnSrid); <=> is null-safe so an unrestricted<->restricted
              -- change (NULL on one side) is detected the same as a value change on both sides.
              OR (SchemaSmith_SupportsColumnSrid() = 1
                  AND NOT (SchemaSmith_ColumnSrid(p_DatabaseName, SchemaSmith_StripBacktickWrapping(c.TableName), SchemaSmith_StripBacktickWrapping(c.ColumnName)) <=> c.Srid))
              -- Comment differs (symmetric: covers added, changed, and cleared -- a declared NULL
              -- comment against a live comment counts as a difference the same as a value change).
              OR (BINARY COALESCE(isc.COLUMN_COMMENT, '') != BINARY COALESCE(c.Comment, ''))
              -- ON UPDATE CURRENT_TIMESTAMP[(n)] differs (symmetric: added, changed -- e.g. a precision
              -- change --, or removed). No SchemaSmith_Supports... gate: unlike Invisible/Srid above,
              -- this clause predates both engines' hard floors, so it is always legal to compare/emit.
              OR (BINARY COALESCE(SchemaSmith_ColumnOnUpdateClause(isc.EXTRA), '') != BINARY COALESCE(c.OnUpdateCurrentTimestamp, ''))
          );

        -- #363: WhatIf twin of the ELSE-branch 'column'/'modified' audit; same source/predicate, wouldModify.
        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'column', CONCAT(SchemaSmith_StripBacktickWrapping(c.TableName), '.', SchemaSmith_StripBacktickWrapping(c.ColumnName)), 'wouldModify'
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
          -- A desired expression-form default this target cannot legally MODIFY into (below MySQL
          -- 8.0.13 / see the degrade guard above) drops the whole column out of this pass; its
          -- current default is left in place rather than emitting a MODIFY that would hit a syntax error.
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
          AND (
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN BINARY SchemaSmith_UpperDataType(isc.COLUMN_TYPE) != BINARY SchemaSmith_UpperDataType(c.DataType)
                   ELSE SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC'))
                     != SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')) END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NULL OR BINARY SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END)
                  -- A DECIMAL default comes back at the column's scale ('0' declared, '0.00' stored), which
                  -- never matched as text and re-ALTERed the column on every deploy.
                  AND SchemaSmith_NumericDefaultsEqual(SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT),
                                                      c.DefaultValue, isc.DATA_TYPE) = 0)
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NOT NULL)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
              -- Invisible-column visibility differs. Gated behind SchemaSmith_SupportsInvisibleColumn(): below
              -- the floor (MySQL 8.0.23 / MariaDB 10.3) the column can never actually become invisible (the
              -- degrade guard above already reports it), so ignore the difference there or it churns every run.
              -- Symmetric in both directions -- a declared column newly marked invisible (visible -> invisible)
              -- and one whose Invisible flag was removed (invisible -> visible) both trip this <> compare.
              OR (SchemaSmith_SupportsInvisibleColumn() = 1 AND (isc.EXTRA LIKE '%INVISIBLE%') <> (c.IsInvisible = 1))
              -- Column SRID differs. Gated behind SchemaSmith_SupportsColumnSrid() (MySQL 8.0.3+; MariaDB
              -- never -- the degrade guard above already reports it there), so ignore the difference below
              -- the floor or it churns every run. isc.SRS_ID cannot be read directly here (absent on
              -- MariaDB -- see SchemaSmith_ColumnSrid); <=> is null-safe so an unrestricted<->restricted
              -- change (NULL on one side) is detected the same as a value change on both sides.
              OR (SchemaSmith_SupportsColumnSrid() = 1
                  AND NOT (SchemaSmith_ColumnSrid(p_DatabaseName, SchemaSmith_StripBacktickWrapping(c.TableName), SchemaSmith_StripBacktickWrapping(c.ColumnName)) <=> c.Srid))
              -- Comment differs (symmetric: covers added, changed, and cleared -- a declared NULL
              -- comment against a live comment counts as a difference the same as a value change).
              OR (BINARY COALESCE(isc.COLUMN_COMMENT, '') != BINARY COALESCE(c.Comment, ''))
              -- ON UPDATE CURRENT_TIMESTAMP[(n)] differs (symmetric: added, changed -- e.g. a precision
              -- change --, or removed). No SchemaSmith_Supports... gate: unlike Invisible/Srid above,
              -- this clause predates both engines' hard floors, so it is always legal to compare/emit.
              OR (BINARY COALESCE(SchemaSmith_ColumnOnUpdateClause(isc.EXTRA), '') != BINARY COALESCE(c.OnUpdateCurrentTimestamp, ''))
          );
    ELSE
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Modify columns');

        -- Per-column progress messages, set-based (preserves the per-column "ALTER TABLE ...
        -- MODIFY COLUMN ..." single-column text, even though execution below folds multiple
        -- columns of the same table into one statement).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Modify column: ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName,
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
          -- A desired expression-form default this target cannot legally MODIFY into (below MySQL
          -- 8.0.13 / see the degrade guard above) drops the whole column out of this pass; its
          -- current default is left in place rather than emitting a MODIFY that would hit a syntax error.
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
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
                   ELSE SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC'))
                     != SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')) END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              -- Default value changes (strip outer single quotes from JSON default for comparison,
              -- since GenerateTableJson wraps string/enum defaults in quotes for DDL but INFORMATION_SCHEMA stores raw values)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NULL OR BINARY SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END)
                  -- A DECIMAL default comes back at the column's scale ('0' declared, '0.00' stored), which
                  -- never matched as text and re-ALTERed the column on every deploy.
                  AND SchemaSmith_NumericDefaultsEqual(SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT),
                                                      c.DefaultValue, isc.DATA_TYPE) = 0)
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NOT NULL)
              -- Collation changes (only when JSON specifies a collation)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              -- Generated expression changes (both sides are generated, but expression differs)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              -- AUTO_INCREMENT removal/addition (live EXTRA vs declared IsAutoIncrement) — parity with identity removal
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
              -- Invisible-column visibility differs. Gated behind SchemaSmith_SupportsInvisibleColumn(): below
              -- the floor (MySQL 8.0.23 / MariaDB 10.3) the column can never actually become invisible (the
              -- degrade guard above already reports it), so ignore the difference there or it churns every run.
              -- Symmetric in both directions -- a declared column newly marked invisible (visible -> invisible)
              -- and one whose Invisible flag was removed (invisible -> visible) both trip this <> compare.
              OR (SchemaSmith_SupportsInvisibleColumn() = 1 AND (isc.EXTRA LIKE '%INVISIBLE%') <> (c.IsInvisible = 1))
              -- Column SRID differs. Gated behind SchemaSmith_SupportsColumnSrid() (MySQL 8.0.3+; MariaDB
              -- never -- the degrade guard above already reports it there), so ignore the difference below
              -- the floor or it churns every run. isc.SRS_ID cannot be read directly here (absent on
              -- MariaDB -- see SchemaSmith_ColumnSrid); <=> is null-safe so an unrestricted<->restricted
              -- change (NULL on one side) is detected the same as a value change on both sides.
              OR (SchemaSmith_SupportsColumnSrid() = 1
                  AND NOT (SchemaSmith_ColumnSrid(p_DatabaseName, SchemaSmith_StripBacktickWrapping(c.TableName), SchemaSmith_StripBacktickWrapping(c.ColumnName)) <=> c.Srid))
              -- Comment differs (symmetric: covers added, changed, and cleared -- a declared NULL
              -- comment against a live comment counts as a difference the same as a value change).
              OR (BINARY COALESCE(isc.COLUMN_COMMENT, '') != BINARY COALESCE(c.Comment, ''))
              -- ON UPDATE CURRENT_TIMESTAMP[(n)] differs (symmetric: added, changed -- e.g. a precision
              -- change --, or removed). No SchemaSmith_Supports... gate: unlike Invisible/Srid above,
              -- this clause predates both engines' hard floors, so it is always legal to compare/emit.
              OR (BINARY COALESCE(SchemaSmith_ColumnOnUpdateClause(isc.EXTRA), '') != BINARY COALESCE(c.OnUpdateCurrentTimestamp, ''))
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
          -- A desired expression-form default this target cannot legally MODIFY into (below MySQL
          -- 8.0.13 / see the degrade guard above) drops the whole column out of this pass; its
          -- current default is left in place rather than emitting a MODIFY that would hit a syntax error.
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
          AND (
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN BINARY SchemaSmith_UpperDataType(isc.COLUMN_TYPE) != BINARY SchemaSmith_UpperDataType(c.DataType)
                   ELSE SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC'))
                     != SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')) END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NULL OR BINARY SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END)
                  -- A DECIMAL default comes back at the column's scale ('0' declared, '0.00' stored), which
                  -- never matched as text and re-ALTERed the column on every deploy.
                  AND SchemaSmith_NumericDefaultsEqual(SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT),
                                                      c.DefaultValue, isc.DATA_TYPE) = 0)
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NOT NULL)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
              -- Invisible-column visibility differs. Gated behind SchemaSmith_SupportsInvisibleColumn(): below
              -- the floor (MySQL 8.0.23 / MariaDB 10.3) the column can never actually become invisible (the
              -- degrade guard above already reports it), so ignore the difference there or it churns every run.
              -- Symmetric in both directions -- a declared column newly marked invisible (visible -> invisible)
              -- and one whose Invisible flag was removed (invisible -> visible) both trip this <> compare.
              OR (SchemaSmith_SupportsInvisibleColumn() = 1 AND (isc.EXTRA LIKE '%INVISIBLE%') <> (c.IsInvisible = 1))
              -- Column SRID differs. Gated behind SchemaSmith_SupportsColumnSrid() (MySQL 8.0.3+; MariaDB
              -- never -- the degrade guard above already reports it there), so ignore the difference below
              -- the floor or it churns every run. isc.SRS_ID cannot be read directly here (absent on
              -- MariaDB -- see SchemaSmith_ColumnSrid); <=> is null-safe so an unrestricted<->restricted
              -- change (NULL on one side) is detected the same as a value change on both sides.
              OR (SchemaSmith_SupportsColumnSrid() = 1
                  AND NOT (SchemaSmith_ColumnSrid(p_DatabaseName, SchemaSmith_StripBacktickWrapping(c.TableName), SchemaSmith_StripBacktickWrapping(c.ColumnName)) <=> c.Srid))
              -- Comment differs (symmetric: covers added, changed, and cleared -- a declared NULL
              -- comment against a live comment counts as a difference the same as a value change).
              OR (BINARY COALESCE(isc.COLUMN_COMMENT, '') != BINARY COALESCE(c.Comment, ''))
              -- ON UPDATE CURRENT_TIMESTAMP[(n)] differs (symmetric: added, changed -- e.g. a precision
              -- change --, or removed). No SchemaSmith_Supports... gate: unlike Invisible/Srid above,
              -- this clause predates both engines' hard floors, so it is always legal to compare/emit.
              OR (BINARY COALESCE(SchemaSmith_ColumnOnUpdateClause(isc.EXTRA), '') != BINARY COALESCE(c.OnUpdateCurrentTimestamp, ''))
          );

        -- Materialize: fold each table's column modifications into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ModifyColStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_ModifyColStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_ModifyColStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', c.TableName, ' ',
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
          -- A desired expression-form default this target cannot legally MODIFY into (below MySQL
          -- 8.0.13 / see the degrade guard above) drops the whole column out of this pass; its
          -- current default is left in place rather than emitting a MODIFY that would hit a syntax error.
          AND NOT (c.IsAutoIncrement = 0 AND c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) LIKE '(%' AND SchemaSmith_SupportsDefaultExpression() = 0)
          AND (
              CASE WHEN UPPER(c.DataType) LIKE 'ENUM%' OR UPPER(c.DataType) LIKE 'SET%'
                     OR UPPER(isc.COLUMN_TYPE) LIKE 'ENUM%' OR UPPER(isc.COLUMN_TYPE) LIKE 'SET%'
                   THEN BINARY SchemaSmith_UpperDataType(isc.COLUMN_TYPE) != BINARY SchemaSmith_UpperDataType(c.DataType)
                   ELSE SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(isc.COLUMN_TYPE), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC'))
                     != SchemaSmith_StripIntDisplayWidth(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')) END
              OR (isc.IS_NULLABLE = 'YES' AND c.IsNullable = 0)
              OR (isc.IS_NULLABLE = 'NO' AND c.IsNullable = 1)
              OR (c.DefaultValue IS NOT NULL AND TRIM(c.DefaultValue) != '' AND c.IsAutoIncrement = 0
                  AND (SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NULL OR BINARY SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) != BINARY
                      CASE WHEN c.DefaultValue LIKE '''%'''
                           THEN REPLACE(SUBSTRING(c.DefaultValue, 2, CHAR_LENGTH(c.DefaultValue) - 2), '''''', '''')
                           ELSE c.DefaultValue END)
                  -- A DECIMAL default comes back at the column's scale ('0' declared, '0.00' stored), which
                  -- never matched as text and re-ALTERed the column on every deploy.
                  AND SchemaSmith_NumericDefaultsEqual(SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT),
                                                      c.DefaultValue, isc.DATA_TYPE) = 0)
              OR ((c.DefaultValue IS NULL OR TRIM(c.DefaultValue) = '') AND SchemaSmith_NormalizeColumnDefault(isc.COLUMN_DEFAULT) IS NOT NULL)
              OR (c.Collation IS NOT NULL AND TRIM(c.Collation) != '' AND BINARY isc.COLLATION_NAME != BINARY c.Collation)
              OR (c.GeneratedExpression IS NOT NULL AND TRIM(c.GeneratedExpression) != ''
                  AND (isc.GENERATION_EXPRESSION IS NULL OR BINARY TRIM(isc.GENERATION_EXPRESSION) != BINARY TRIM(c.GeneratedExpression)))
              OR ((isc.EXTRA LIKE '%auto_increment%') <> (c.IsAutoIncrement = 1))
              -- Invisible-column visibility differs. Gated behind SchemaSmith_SupportsInvisibleColumn(): below
              -- the floor (MySQL 8.0.23 / MariaDB 10.3) the column can never actually become invisible (the
              -- degrade guard above already reports it), so ignore the difference there or it churns every run.
              -- Symmetric in both directions -- a declared column newly marked invisible (visible -> invisible)
              -- and one whose Invisible flag was removed (invisible -> visible) both trip this <> compare.
              OR (SchemaSmith_SupportsInvisibleColumn() = 1 AND (isc.EXTRA LIKE '%INVISIBLE%') <> (c.IsInvisible = 1))
              -- Column SRID differs. Gated behind SchemaSmith_SupportsColumnSrid() (MySQL 8.0.3+; MariaDB
              -- never -- the degrade guard above already reports it there), so ignore the difference below
              -- the floor or it churns every run. isc.SRS_ID cannot be read directly here (absent on
              -- MariaDB -- see SchemaSmith_ColumnSrid); <=> is null-safe so an unrestricted<->restricted
              -- change (NULL on one side) is detected the same as a value change on both sides.
              OR (SchemaSmith_SupportsColumnSrid() = 1
                  AND NOT (SchemaSmith_ColumnSrid(p_DatabaseName, SchemaSmith_StripBacktickWrapping(c.TableName), SchemaSmith_StripBacktickWrapping(c.ColumnName)) <=> c.Srid))
              -- Comment differs (symmetric: covers added, changed, and cleared -- a declared NULL
              -- comment against a live comment counts as a difference the same as a value change).
              OR (BINARY COALESCE(isc.COLUMN_COMMENT, '') != BINARY COALESCE(c.Comment, ''))
              -- ON UPDATE CURRENT_TIMESTAMP[(n)] differs (symmetric: added, changed -- e.g. a precision
              -- change --, or removed). No SchemaSmith_Supports... gate: unlike Invisible/Srid above,
              -- this clause predates both engines' hard floors, so it is always legal to compare/emit.
              OR (BINARY COALESCE(SchemaSmith_ColumnOnUpdateClause(isc.EXTRA), '') != BINARY COALESCE(c.OnUpdateCurrentTimestamp, ''))
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
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`',
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
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`',
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
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(c.TableName), '` ',
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
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', SchemaSmith_StripBacktickWrapping(c.TableName), '` ',
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
        SELECT CONNECTION_ID(), 'column', CONCAT(p_DatabaseName, '.', TableName, '.', ColumnName), 'dropSuppressed'
        FROM _SchemaSmith_WouldDropColumns;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropColumns;
    END IF;

    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop unused columns');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` DROP COLUMN `', ColumnName, '`')
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
        SELECT CONNECTION_ID(), CONCAT('  Drop FK for column: ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` DROP FOREIGN KEY `', ConstraintName, '`')
        FROM _SchemaSmith_FKsToDrop;

        -- Materialize: fold each table's FK drops into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_FKDropForColStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_FKDropForColStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_FKDropForColStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
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

        -- Check if constraint references this column (explicit COLLATE to avoid collation mismatch).
        -- INFORMATION_SCHEMA.CHECK_CONSTRAINTS does not exist on MySQL 5.7 and MySQL binds
        -- INFORMATION_SCHEMA references at CREATE time, so the read lives only inside this
        -- dynamically-built string, gated by SchemaSmith_SupportsCheckConstraints() (see
        -- GenerateTableJson for the full rationale). Below the floor there are no check
        -- constraints to drop, so _SchemaSmith_CKsToDrop simply stays unpopulated by this step.
        IF SchemaSmith_SupportsCheckConstraints() = 1 THEN
            SET @v_ckDbName = p_DatabaseName;
            SET @v_ckSql = 'INSERT IGNORE INTO _SchemaSmith_CKsToDrop (TableName, ConstraintName)
SELECT DISTINCT
    CONVERT(tc.TABLE_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci,
    CONVERT(cc.CONSTRAINT_NAME USING utf8mb4) COLLATE utf8mb4_unicode_ci
FROM _SchemaSmith_ColumnsToDrop ctd
INNER JOIN INFORMATION_SCHEMA.CHECK_CONSTRAINTS cc
    ON CONVERT(cc.CONSTRAINT_SCHEMA USING utf8mb4) = CONVERT(@v_ckDbName USING utf8mb4)
    AND CONVERT(cc.CHECK_CLAUSE USING utf8mb4) COLLATE utf8mb4_unicode_ci
        LIKE CONCAT(''%`'', ctd.ColumnName COLLATE utf8mb4_unicode_ci, ''`%'')
INNER JOIN INFORMATION_SCHEMA.TABLE_CONSTRAINTS tc
    ON CONVERT(tc.CONSTRAINT_SCHEMA USING utf8mb4) = CONVERT(cc.CONSTRAINT_SCHEMA USING utf8mb4)
    AND CONVERT(tc.CONSTRAINT_NAME USING utf8mb4) = CONVERT(cc.CONSTRAINT_NAME USING utf8mb4)
    AND CONVERT(tc.TABLE_NAME USING utf8mb4) = CONVERT(ctd.TableName USING utf8mb4)
    AND tc.CONSTRAINT_TYPE = ''CHECK''';
            PREPARE stmt FROM @v_ckSql;
            EXECUTE stmt;
            DEALLOCATE PREPARE stmt;
        END IF;

        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('  Drop check constraint for column: ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` DROP CONSTRAINT `', ConstraintName, '`')
        FROM _SchemaSmith_CKsToDrop;

        -- Materialize: fold each table's check-constraint drops into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_CKDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_CKDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_CKDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
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
        SELECT CONNECTION_ID(), CONCAT('  Drop index for column: DROP INDEX `', IndexName, '` ON `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`')
        FROM _SchemaSmith_IdxsToDrop;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_IdxDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_IdxDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_IdxDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
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
        SELECT CONNECTION_ID(), CONCAT('  Drop generated column: ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                      '` DROP COLUMN `', ColumnName, '`')
        FROM _SchemaSmith_GenColsToDrop;

        -- Materialize: fold each table's generated-column drops into one multi-clause ALTER, then drain.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_GenColDropStmts;
        CREATE TEMPORARY TABLE _SchemaSmith_GenColDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_GenColDropStmts (Stmt)
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
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
        SELECT CONNECTION_ID(), CONCAT('  Drop column: ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`',
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
        SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`',
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
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ENGINE = ', t.Engine)
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
                SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ENGINE = ', t.Engine) AS AlterEngineStatement
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
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
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
                SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
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
    -- STEP 5.5: ALTER TABLE COMMENT
    -- =======================
    -- Symmetric compare (unlike Engine/RowFormat above, which only ever apply a declared value and
    -- never clear one): a declared NULL comment against a live comment counts as a difference the
    -- same as a value change, so removing a Comment from the JSON clears it in the database too --
    -- matching the symmetric column-comment predicate in STEP 3 above. Escaping matches the
    -- established _SchemaSmith_FullTextIndexes.Comment form (double the embedded single quotes).
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table comment');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
                      ' COMMENT=''', REPLACE(COALESCE(t.Comment, ''), '''', ''''''), '''')
        FROM _SchemaSmith_Tables t
        INNER JOIN INFORMATION_SCHEMA.TABLES ist
            ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
            AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
        WHERE t.NewTable = 0
          AND BINARY COALESCE(ist.TABLE_COMMENT, '') != BINARY COALESCE(t.Comment, '');
    ELSE
        BEGIN
            DECLARE v_CommentDone INT DEFAULT FALSE;
            DECLARE v_CommentSql TEXT;
            DECLARE cur_CommentChanges CURSOR FOR
                SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName,
                              ' COMMENT=''', REPLACE(COALESCE(t.Comment, ''), '''', ''''''), '''') AS AlterCommentStatement
                FROM _SchemaSmith_Tables t
                INNER JOIN INFORMATION_SCHEMA.TABLES ist
                    ON BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName
                    AND BINARY ist.TABLE_NAME = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
                WHERE t.NewTable = 0
                  AND BINARY COALESCE(ist.TABLE_COMMENT, '') != BINARY COALESCE(t.Comment, '');

            DECLARE CONTINUE HANDLER FOR NOT FOUND SET v_CommentDone = TRUE;

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table comment');
            SET v_CommentDone = FALSE;
            OPEN cur_CommentChanges;

            comment_changes_loop: LOOP
                FETCH cur_CommentChanges INTO v_CommentSql;
                IF v_CommentDone THEN
                    LEAVE comment_changes_loop;
                END IF;

                INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), CONCAT('  Change comment: ', v_CommentSql));
                SET @exec_sql = v_CommentSql;
                PREPARE stmt FROM @exec_sql;
                EXECUTE stmt;
                DEALLOCATE PREPARE stmt;
            END LOOP;

            CLOSE cur_CommentChanges;
        END;
    END IF;

    -- =======================
    -- STEP 6: ALTER TABLE ROW_FORMAT
    -- =======================
    -- Alter table row format if different
    IF p_WhatIf = 1 THEN
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Change table row format');
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ROW_FORMAT=', t.RowFormat)
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
                SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' ROW_FORMAT=', t.RowFormat) AS AlterRowFormatStatement
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
        SELECT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' AUTO_INCREMENT=', t.AutoIncrementValue)
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
                SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.', t.TableName, ' AUTO_INCREMENT=', t.AutoIncrementValue) AS AlterAutoIncStatement
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
            SELECT 1 FROM _SchemaSmith_ExistingTables ist
            WHERE BINARY ist.TableName = BINARY SchemaSmith_StripBacktickWrapping(t.TableName)
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
    -- PreventDrop) to the ChangeAudit seam as 'dropSuppressed' so the run can surface a manifest. Same
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
              SELECT 1 FROM _SchemaSmith_ExistingTables ist
              WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_Tables t
              WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
          );

        INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
        SELECT CONNECTION_ID(), 'table', CONCAT(p_DatabaseName, '.', TableName), 'dropSuppressed'
        FROM _SchemaSmith_WouldDropTables;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_WouldDropTables;
    END IF;

    -- =======================
    -- STEP 8: DROP TABLES REMOVED FROM PRODUCT
    -- =======================
    -- Drop tables that are owned by this product but no longer in the definition
    IF p_DropTablesRemovedFromProduct = 1 THEN
        -- Data-loss guard: a partitioned table spreads data across partitions that DROP TABLE
        -- destroys outright. SchemaSmith has no partitioning support -- partitioning only happens
        -- by hand, typically once a table has grown -- so an ordinary product-owned table can be
        -- partitioned after deployment and later look like an ordinary drop-by-absence candidate.
        -- Fail closed before any DDL below, in both live and WhatIf mode, mirroring the
        -- UnsupportedFeaturePolicy=fail SIGNAL pattern used elsewhere in this proc. Table names are
        -- logged individually first (the SIGNAL MESSAGE_TEXT below stays well under MySQL's 128-char
        -- limit -- see STEP 4's comment on that limit).
        INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
        SELECT DISTINCT CONNECTION_ID(), CONCAT('  Partitioned table removed from product, not dropped (data-loss guard): ', po.ObjectName)
        FROM SchemaSmith_ProductOwnership po
        INNER JOIN INFORMATION_SCHEMA.PARTITIONS ip
            ON CONVERT(ip.TABLE_SCHEMA USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
           AND CONVERT(ip.TABLE_NAME USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
           AND ip.PARTITION_NAME IS NOT NULL
        WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
          AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
          AND po.ObjectType = 'TABLE'
          AND COALESCE(po.PreventDrop, 0) = 0
          AND EXISTS (
              SELECT 1 FROM _SchemaSmith_ExistingTables ist
              WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
          )
          AND NOT EXISTS (
              SELECT 1 FROM _SchemaSmith_Tables t
              WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
          );

        IF ROW_COUNT() > 0 THEN
            SET @ss_msg = 'Partitioned table(s) skipped by drop-by-absence guard; drop manually or mark PreventDrop.';
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = @ss_msg;
        END IF;

        -- #289: drop any foreign key that REFERENCES a table about to be removed BEFORE the table
        -- drop below, otherwise the DROP TABLE fails on a still-present inbound dependency.
        IF p_WhatIf = 1 THEN
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop inbound foreign keys referencing tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT DISTINCT CONNECTION_ID(), CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`',
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
                  SELECT 1 FROM _SchemaSmith_ExistingTables ist
                  WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
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
                  SELECT 1 FROM _SchemaSmith_ExistingTables ist
                  WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );

            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message) VALUES (CONNECTION_ID(), 'Drop inbound foreign keys referencing tables removed from product');
            INSERT INTO SchemaSmith_StatusMessages (SessionId, Message)
            SELECT CONNECTION_ID(), CONCAT('  Drop inbound FK: ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName,
                          '` DROP FOREIGN KEY `', ConstraintName, '`')
            FROM _SchemaSmith_InboundFKsToDrop;

            -- Materialize: fold each table's inbound FK drops into one multi-clause ALTER, then drain.
            DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_InboundFKDropStmts;
            CREATE TEMPORARY TABLE _SchemaSmith_InboundFKDropStmts (RowId INT AUTO_INCREMENT PRIMARY KEY, Stmt TEXT)
                ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
            INSERT INTO _SchemaSmith_InboundFKDropStmts (Stmt)
            SELECT CONCAT('ALTER TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '` ',
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
                        THEN CONCAT('CALL SchemaSmith_CustomTableDrop(''', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, ''', ''', po.ObjectName, ''')')
                        ELSE CONCAT('DROP TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', po.ObjectName, '`')
                        END
            FROM SchemaSmith_ProductOwnership po
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND COALESCE(po.PreventDrop, 0) = 0
              -- Table exists
              AND EXISTS (
                  SELECT 1 FROM _SchemaSmith_ExistingTables ist
                  WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
              -- Not in current definition
              AND NOT EXISTS (
                  SELECT 1 FROM _SchemaSmith_Tables t
                  WHERE CONVERT(SchemaSmith_StripBacktickWrapping(t.TableName) USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              );

            -- #363: WhatIf twin of the ELSE-branch 'table'/'dropped' audit; same predicate, ObjectName
            -- is po.ObjectName (= _SchemaSmith_TablesToDrop.TableName in the real branch).
            INSERT INTO SchemaSmith_ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
            SELECT CONNECTION_ID(), 'table', po.ObjectName, 'wouldDrop'
            FROM SchemaSmith_ProductOwnership po
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND COALESCE(po.PreventDrop, 0) = 0
              AND EXISTS (
                  SELECT 1 FROM _SchemaSmith_ExistingTables ist
                  WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
              )
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
                     THEN CONCAT('CALL SchemaSmith_CustomTableDrop(''', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, ''', ''', po.ObjectName, ''')')
                     ELSE CONCAT('DROP TABLE `', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', po.ObjectName, '`')
                     END
            FROM SchemaSmith_ProductOwnership po
            WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
              AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
              AND po.ObjectType = 'TABLE'
              AND COALESCE(po.PreventDrop, 0) = 0
              -- Table exists
              AND EXISTS (
                  SELECT 1 FROM _SchemaSmith_ExistingTables ist
                  WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4)
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
                              GROUP_CONCAT(CONCAT('`', CONVERT(p_DatabaseName USING utf8mb4) COLLATE utf8mb4_unicode_ci, '`.`', TableName, '`') ORDER BY TableName SEPARATOR ', '))
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
          AND EXISTS (SELECT 1 FROM _SchemaSmith_ExistingTables ist
                       WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4))
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
        -- Rebuild the existing-table snapshot to the POST-drop state: the prune must see tables STEP 8 just
        -- dropped (and any dropped out-of-band) as gone, so their ownership rows are reconciled here.
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_ExistingTables;
        CREATE TEMPORARY TABLE _SchemaSmith_ExistingTables (
            TableName VARCHAR(128) NOT NULL,
            PRIMARY KEY (TableName)
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_ExistingTables (TableName)
        SELECT CONVERT(ist.TABLE_NAME USING utf8mb4)
        FROM INFORMATION_SCHEMA.TABLES ist
        WHERE BINARY ist.TABLE_SCHEMA = BINARY p_DatabaseName;

        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_OrphanedOwnership;
        CREATE TEMPORARY TABLE _SchemaSmith_OrphanedOwnership (Id INT PRIMARY KEY)
            ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
        INSERT INTO _SchemaSmith_OrphanedOwnership (Id)
        SELECT po.Id
          FROM SchemaSmith_ProductOwnership po
          WHERE CONVERT(po.ProductName USING utf8mb4) = CONVERT(p_ProductName USING utf8mb4)
            AND CONVERT(po.ObjectSchema USING utf8mb4) = CONVERT(p_DatabaseName USING utf8mb4)
            AND po.ObjectType = 'TABLE'
            AND NOT EXISTS (SELECT 1 FROM _SchemaSmith_ExistingTables ist
                             WHERE CONVERT(ist.TableName USING utf8mb4) = CONVERT(po.ObjectName USING utf8mb4));
        DELETE po FROM SchemaSmith_ProductOwnership po
          JOIN _SchemaSmith_OrphanedOwnership o ON o.Id = po.Id;
        DROP TEMPORARY TABLE IF EXISTS _SchemaSmith_OrphanedOwnership;
    END IF;

END//

DELIMITER ;
