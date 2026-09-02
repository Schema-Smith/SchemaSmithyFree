-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF OBJECT_ID('SchemaSmith.MissingTableAndColumnQuench', 'P') IS NOT NULL DROP PROCEDURE SchemaSmith.MissingTableAndColumnQuench
GO
CREATE PROCEDURE SchemaSmith.MissingTableAndColumnQuench
    @WhatIf BIT = 0
AS
BEGIN TRY
  DECLARE @v_SQL NVARCHAR(MAX) = ''

  -- Filegroup placement (#filegroups): a NEW table declaring a filegroup name that does not exist on this
  -- target must fail loudly BEFORE any DDL runs, naming both the table and the filegroup -- not create the
  -- filegroup (that is the user's job -- provisioning stays out of package portability) and not silently
  -- fall back to the default. Only NewTable=1 rows are checked here; an already-existing table's declared
  -- vs. deployed filegroup is a different question (a possible "move"), handled in ModifiedTableQuench.
  RAISERROR('Validate declared table filegroups exist', 10, 100) WITH NOWAIT
  IF EXISTS (SELECT 1
               FROM #Tables t WITH (NOLOCK)
               WHERE t.NewTable = 1
                 AND t.[FileGroup] IS NOT NULL
                 AND NOT EXISTS (SELECT * FROM sys.filegroups fg WITH (NOLOCK) WHERE fg.[name] = SchemaSmith.fn_StripBracketWrapping(t.[FileGroup])))
  BEGIN
    DECLARE @v_FGTable NVARCHAR(1010), @v_FGName NVARCHAR(500)
    SELECT TOP 1 @v_FGTable = t.[Schema] + '.' + t.[Name], @v_FGName = t.[FileGroup]
      FROM #Tables t WITH (NOLOCK)
      WHERE t.NewTable = 1
        AND t.[FileGroup] IS NOT NULL
        AND NOT EXISTS (SELECT * FROM sys.filegroups fg WITH (NOLOCK) WHERE fg.[name] = SchemaSmith.fn_StripBracketWrapping(t.[FileGroup]))
    RAISERROR('Table %s declares filegroup %s, which does not exist on this database. SchemaSmith does not create filegroups -- create it on the target first, or correct the declared name.', 16, 1, @v_FGTable, @v_FGName)
  END

  -- Same check for the other two placement clauses. A FILESTREAM filegroup must additionally BE one
  -- (type 'FD'): naming an ordinary filegroup there fails with the engine's own message, which does not
  -- say which table asked for it.
  IF EXISTS (SELECT 1 FROM #Tables t WITH (NOLOCK)
              WHERE t.[TextImageFileGroup] IS NOT NULL
                AND NOT EXISTS (SELECT * FROM sys.filegroups fg WITH (NOLOCK)
                                 WHERE fg.[name] = SchemaSmith.fn_StripBracketWrapping(t.[TextImageFileGroup])))
  BEGIN
    DECLARE @v_TiTable NVARCHAR(1010), @v_TiName NVARCHAR(500)
    SELECT TOP 1 @v_TiTable = t.[Schema] + '.' + t.[Name], @v_TiName = t.[TextImageFileGroup]
      FROM #Tables t WITH (NOLOCK)
     WHERE t.[TextImageFileGroup] IS NOT NULL
       AND NOT EXISTS (SELECT * FROM sys.filegroups fg WITH (NOLOCK)
                        WHERE fg.[name] = SchemaSmith.fn_StripBracketWrapping(t.[TextImageFileGroup]))
    RAISERROR('Table %s declares TextImageFileGroup %s, which does not exist on this database. SchemaSmith does not create filegroups -- create it on the target first, or correct the declared name.', 16, 1, @v_TiTable, @v_TiName)
  END

  IF EXISTS (SELECT 1 FROM #Tables t WITH (NOLOCK)
              WHERE t.[FileStreamFileGroup] IS NOT NULL
                AND NOT EXISTS (SELECT * FROM sys.filegroups fg WITH (NOLOCK)
                                 WHERE fg.[name] = SchemaSmith.fn_StripBracketWrapping(t.[FileStreamFileGroup])
                                   AND fg.[type] = 'FD'))
  BEGIN
    DECLARE @v_FsTable NVARCHAR(1010), @v_FsName NVARCHAR(500)
    SELECT TOP 1 @v_FsTable = t.[Schema] + '.' + t.[Name], @v_FsName = t.[FileStreamFileGroup]
      FROM #Tables t WITH (NOLOCK)
     WHERE t.[FileStreamFileGroup] IS NOT NULL
       AND NOT EXISTS (SELECT * FROM sys.filegroups fg WITH (NOLOCK)
                        WHERE fg.[name] = SchemaSmith.fn_StripBracketWrapping(t.[FileStreamFileGroup])
                          AND fg.[type] = 'FD')
    RAISERROR('Table %s declares FileStreamFileGroup %s, which is not a FILESTREAM filegroup on this database. SchemaSmith does not create filegroups -- create it with CONTAINS FILESTREAM on the target first, or correct the declared name.', 16, 1, @v_FsTable, @v_FsName)
  END

  RAISERROR('Handle Table Renames', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STUFF((SELECT CHAR(13) + CHAR(10) + CAST('RAISERROR(''  Rename ' + T.[Schema] + '.' + T.[OldName] + ' to ' + T.[Schema] + '.' + T.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC sp_rename ''' + SchemaSmith.fn_StripBracketWrapping(T.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(T.[OldName]) + ''', ''' + SchemaSmith.fn_StripBracketWrapping(T.[Name]) + ''';' + CHAR(13) + CHAR(10) AS NVARCHAR(MAX))
                           FROM #Tables T WITH (NOLOCK)
                           WHERE OBJECT_ID(T.[Schema] + '.' + T.[OldName]) IS NOT NULL
                             AND OBJECT_ID(T.[Schema] + '.' + T.[Name]) IS NULL
                           FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Handle Column Renames', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STUFF((SELECT CHAR(13) + CHAR(10) + CAST('RAISERROR(''  Rename ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[OldName] + ' to ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC sp_rename ''' + SchemaSmith.fn_StripBracketWrapping(c.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(c.[TableName]) + '.' + SchemaSmith.fn_StripBracketWrapping(c.[OldName]) + ''', ''' + SchemaSmith.fn_StripBracketWrapping(c.[ColumnName]) + ''', ''COLUMN'';' + CHAR(13) + CHAR(10) AS NVARCHAR(MAX))
                           FROM #Columns c WITH (NOLOCK)
                           WHERE COLUMNPROPERTY(OBJECT_ID(c.[Schema] + '.' + c.[TableName]), SchemaSmith.fn_StripBracketWrapping(c.[OldName]), 'AllowsNull') IS NOT NULL
                             AND COLUMNPROPERTY(OBJECT_ID(c.[Schema] + '.' + c.[TableName]), SchemaSmith.fn_StripBracketWrapping(c.[ColumnName]), 'AllowsNull') IS NULL
                           FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  IF OBJECT_ID('SchemaSmith.CustomTableRestore') IS NOT NULL
  BEGIN
    RAISERROR('Attempt custom table restore for tables being added in case they were custom dropped previously', 10, 100) WITH NOWAIT
    SELECT @v_SQL = STUFF((SELECT CHAR(13) + CHAR(10) + CAST('EXEC SchemaSmith.CustomTableRestore ''' + SchemaSmith.fn_StripBracketWrapping(T.[Schema]) + ''', ''' + SchemaSmith.fn_StripBracketWrapping(T.[Name]) + ''';' AS NVARCHAR(MAX))
                             FROM #Tables T WITH (NOLOCK)
                             WHERE NewTable = 1
                             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
    IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

    SELECT @v_SQL = STUFF((SELECT CHAR(13) + CHAR(10) + CAST('RAISERROR(''  ' + T.[Schema] + '.' + T.[Name] + ' Restored'', 10, 100) WITH NOWAIT;' AS NVARCHAR(MAX))
                             FROM #Tables T WITH (NOLOCK)
                             WHERE NewTable = 1
                               AND OBJECT_ID([Schema] + '.' + [Name]) IS NOT NULL
                             FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
    IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

    UPDATE #Tables
      SET NewTable = 0
      WHERE NewTable = 1
        AND OBJECT_ID([Schema] + '.' + [Name]) IS NOT NULL
  END


  -- TEXTIMAGE_ON is REJECTED by SQL Server (error 1709) on a table with no large-object column, and that
  -- message names neither the table nor the property. A template-level declaration would otherwise break
  -- every ordinary table in the package. FILESTREAM columns are deliberately excluded -- 1709 says
  -- "non-FILESTREAM varbinary(max)", and a FILESTREAM column does not satisfy the clause.
  IF EXISTS (SELECT 1 FROM #Tables t WITH (NOLOCK)
              WHERE t.[TextImageFileGroup] IS NOT NULL
                AND NOT EXISTS (SELECT 1 FROM #Columns c WITH (NOLOCK)
                                 WHERE c.[Schema] = t.[Schema] AND c.[TableName] = t.[Name]
                                   AND ISNULL(c.[FileStream], 0) = 0
                                   AND (UPPER(REPLACE(c.[DataType], ' ', '')) LIKE '%(MAX)%'
                                        OR UPPER(c.[DataType]) IN ('TEXT', 'NTEXT', 'IMAGE', 'XML')
                                        OR UPPER(c.[DataType]) LIKE 'XML(%')))
  BEGIN
    DECLARE @v_NoLobTable NVARCHAR(1010)
    SELECT TOP 1 @v_NoLobTable = t.[Schema] + '.' + t.[Name]
      FROM #Tables t WITH (NOLOCK)
     WHERE t.[TextImageFileGroup] IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM #Columns c WITH (NOLOCK)
                        WHERE c.[Schema] = t.[Schema] AND c.[TableName] = t.[Name]
                          AND ISNULL(c.[FileStream], 0) = 0
                          AND (UPPER(REPLACE(c.[DataType], ' ', '')) LIKE '%(MAX)%'
                               OR UPPER(c.[DataType]) IN ('TEXT', 'NTEXT', 'IMAGE', 'XML')
                               OR UPPER(c.[DataType]) LIKE 'XML(%'))
    RAISERROR('Table %s declares TextImageFileGroup but has no large-object column to place. SQL Server rejects TEXTIMAGE_ON on such a table (error 1709). Large-object columns are text, ntext, image, xml, and the (MAX) types -- a FILESTREAM column does not count. Remove TextImageFileGroup, or declare a large-object column.', 16, 1, @v_NoLobTable)
  END
  RAISERROR('Add New Tables', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STUFF((SELECT CHAR(13) + CHAR(10) + CAST('RAISERROR(''  Adding new table ' + T.[Schema] + '.' + T.[Name] +
                                  CASE WHEN RTRIM(ISNULL(T.[VariantName], '')) <> '' THEN ' (variant: ' + REPLACE(RTRIM(T.[VariantName]), '''', '''''') + ')' ELSE '' END + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC(''CREATE TABLE ' + T.[Schema] + '.' + T.[Name] + ' (' + REPLACE(ScriptColumns, '''', '''''') + ')' +
                                  -- Filegroup placement (#filegroups): ON comes right after the column list,
                                  -- BEFORE the WITH clause, per CREATE TABLE's own grammar. Existence was
                                  -- already validated above, so this can emit unconditionally.
                                  -- Graph tables (#graph): AS NODE / AS EDGE follows the column list.
                                  -- Create-time only -- SQL Server has no ALTER for it -- so a change on an
                                  -- existing table is refused in ModifiedTableQuench rather than attempted.
                                  CASE WHEN T.[GraphType] = 'Node' THEN ' AS NODE'
                                       WHEN T.[GraphType] = 'Edge' THEN ' AS EDGE' ELSE '' END +
                                  CASE WHEN T.[FileGroup] IS NOT NULL THEN ' ON ' + T.[FileGroup] ELSE '' END +
                                  -- TEXTIMAGE_ON follows ON, in SQL Server's own clause order. FILESTREAM_ON
                                  -- deliberately does NOT appear here: the FILESTREAM column is withheld from
                                  -- this CREATE (it needs a unique constraint first, see FileStreamColumnQuench),
                                  -- so the table has no FILESTREAM column yet and the clause is rejected.
                                  -- It is applied there instead, by ALTER, before the column is added.
                                  -- Both are create-time only; a change on a deployed table is refused in
                                  -- ModifiedTableQuench rather than silently ignored.
                                  CASE WHEN T.[TextImageFileGroup] IS NOT NULL THEN ' TEXTIMAGE_ON ' + T.[TextImageFileGroup] ELSE '' END +
                                  -- Sparse columns and a COLUMN_SET are incompatible with data compression, and SQL Server 2008
                                  -- REJECTS the clause outright on such a table -- even DATA_COMPRESSION=NONE. Modern servers
                                  -- accept the redundant NONE, so this only fails at the floor, where the XML ingest path runs.
                                  -- One WITH clause, built from whatever applies. Ledger and
                                  -- DATA_COMPRESSION are legal together but must share a single WITH,
                                  -- so each part contributes ', <option>' and the leading comma is
                                  -- stripped once. An empty list emits no WITH at all, which is what
                                  -- keeps a table with neither exactly as it was before ledger existed.
                                  CASE WHEN t.[WithOptions] <> '' THEN ' WITH (' + STUFF(t.[WithOptions], 1, 2, '') + ')' ELSE '' END + ''');' + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''table'', ''' + T.[Schema] + '.' + T.[Name] + ''', ''created'');' AS NVARCHAR(MAX))
                           FROM (SELECT T.[Schema], T.[Name], t.[CompressionType], t.[FileGroup], t.[FileStreamFileGroup], t.[TextImageFileGroup], T.[VariantName], T.[GraphType],
                                        WithOptions =
                                            CASE T.[Ledger] WHEN 'AppendOnly' THEN ', LEDGER = ON (APPEND_ONLY = ON)'
                                                            WHEN 'Updatable'  THEN ', SYSTEM_VERSIONING = ON, LEDGER = ON'
                                                            ELSE '' END +
                                            -- Sparse columns and a COLUMN_SET are incompatible with data compression, and SQL
                                            -- Server 2008 REJECTS the clause outright on such a table -- even DATA_COMPRESSION=NONE.
                                            CASE WHEN NOT EXISTS (SELECT 1 FROM #Columns C2 WITH (NOLOCK)
                                                                   WHERE C2.[Schema] = T.[Schema] AND C2.[TableName] = T.[Name]
                                                                     AND (ISNULL(C2.[Sparse], 0) = 1 OR ISNULL(C2.[IsColumnSet], 0) = 1))
                                                      AND ISNULL(T.[CompressionType], 'NONE') IN ('NONE', 'ROW', 'PAGE')
                                                 THEN ', DATA_COMPRESSION=' + ISNULL(T.[CompressionType], 'NONE') ELSE '' END,
                                        HasSparseOrColumnSet = CASE WHEN EXISTS (SELECT 1 FROM #Columns C2 WITH (NOLOCK)
                                                                                  WHERE C2.[Schema] = T.[Schema] AND C2.[TableName] = T.[Name]
                                                                                    AND (ISNULL(C2.[Sparse], 0) = 1 OR ISNULL(C2.[IsColumnSet], 0) = 1)) THEN 1 ELSE 0 END,
                                        ScriptColumns = STUFF((SELECT ', ' + [ColumnScript] FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND RTRIM(ISNULL([ComputedExpression], '')) = '' AND ISNULL(C.[FileStream], 0) = 0 ORDER BY c.[_RowId] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
                                   FROM #Tables T WITH (NOLOCK)
                                   WHERE NewTable = 1) T
                           FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  -- Object-change audit (#363): WhatIf twin of the embedded 'table'/'created' row above. That row
  -- rides the CREATE TABLE DDL (executed only on a real run); under WhatIf the DDL is printed, so
  -- capture the would-create here from the same #Tables state.
  IF @WhatIf = 1
    INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
      SELECT @@SPID, 'table', T.[Schema] + '.' + T.[Name], 'wouldCreate'
        FROM #Tables T WITH (NOLOCK) WHERE NewTable = 1

  RAISERROR('Add New Physical Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STUFF((SELECT CHAR(13) + CHAR(10) + CAST('RAISERROR(''  Adding ' + CAST(ColumnCount AS NVARCHAR(100)) + ' new columns to ' + T.[Schema] + '.' + T.[Name] +
                                  CASE WHEN RTRIM(ISNULL(VariantList, '')) <> '' THEN ' (variant: ' + VariantList + ')' ELSE '' END + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' ADD ' + ColumnScripts + ';' AS NVARCHAR(MAX))
                           FROM (SELECT T.[Schema], T.[Name],
                                        ColumnScripts = STUFF((SELECT ', ' + CAST([ColumnScript] +
                                                              -- WITH VALUES is an ALTER-only clause, and it is per COLUMN, not per statement:
                                                              -- one column in a multi-column ADD can carry it while its neighbours do not
                                                              -- (verified live). It must follow DEFAULT, which is the last clause ColumnScript
                                                              -- emits. Guarded on a default being present because WITH VALUES without one is a
                                                              -- SYNTAX error, not a no-op -- it would fail the whole batch. A column authored
                                                              -- that way is reported by --Validate rather than silently dropped here.
                                                              CASE WHEN ISNULL(c.[BackfillExistingRows], 0) = 1 AND RTRIM(ISNULL(c.[Default], '')) <> ''
                                                                   THEN ' WITH VALUES' ELSE '' END
                                                         AS NVARCHAR(MAX)) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '' AND ISNULL(C.[FileStream], 0) = 0 ORDER BY c.[_RowId] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
                                        ColumnCount = (SELECT COUNT(*) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '' AND ISNULL(C.[FileStream], 0) = 0),
                                        VariantList = STUFF((SELECT ', ' + CAST(REPLACE(RTRIM(c.[VariantName]), '''', '''''') AS NVARCHAR(MAX))
                                                               FROM #Columns C WITH (NOLOCK)
                                                               WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '' AND ISNULL(C.[FileStream], 0) = 0
                                                                 AND RTRIM(ISNULL(c.[VariantName], '')) <> '' FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
                                   FROM #Tables T WITH (NOLOCK)
                                   WHERE NewTable = 0
                                     AND EXISTS (SELECT * FROM #Columns c WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '' AND ISNULL(C.[FileStream], 0) = 0)) T
                           FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  -- Object-change audit (#243 E5, #363): one row per physical column added to an EXISTING table. New
  -- tables' columns are covered by the table/created row above, so NewTable = 0 only. Per-source-row
  -- (the ALTER above folds a table's new columns into one statement, so this cannot weave into it).
  -- Runs regardless of @WhatIf so a WhatIf preview is captured; the action carries the mode.
  INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
    SELECT @@SPID, 'column', c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName], CASE WHEN @WhatIf = 1 THEN 'wouldCreate' ELSE 'created' END
      FROM #Columns c WITH (NOLOCK)
      JOIN #Tables t WITH (NOLOCK) ON t.[Schema] = c.[Schema] AND t.[Name] = c.[TableName]
      WHERE t.NewTable = 0 AND c.NewColumn = 1 AND RTRIM(ISNULL(c.[ComputedExpression], '')) = '' AND ISNULL(c.[FileStream], 0) = 0

  SET NOCOUNT OFF
END TRY
BEGIN CATCH
    DECLARE @v_RethrowMsg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@v_RethrowMsg, 16, 1);
END CATCH