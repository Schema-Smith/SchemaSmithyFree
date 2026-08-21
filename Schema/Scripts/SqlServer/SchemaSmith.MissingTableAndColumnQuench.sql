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

  RAISERROR('Add New Tables', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STUFF((SELECT CHAR(13) + CHAR(10) + CAST('RAISERROR(''  Adding new table ' + T.[Schema] + '.' + T.[Name] +
                                  CASE WHEN RTRIM(ISNULL(T.[VariantName], '')) <> '' THEN ' (variant: ' + REPLACE(RTRIM(T.[VariantName]), '''', '''''') + ')' ELSE '' END + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC(''CREATE TABLE ' + T.[Schema] + '.' + T.[Name] + ' (' + REPLACE(ScriptColumns, '''', '''''') + ')' +
                                  -- Filegroup placement (#filegroups): ON comes right after the column list,
                                  -- BEFORE the WITH clause, per CREATE TABLE's own grammar. Existence was
                                  -- already validated above, so this can emit unconditionally.
                                  CASE WHEN T.[FileGroup] IS NOT NULL THEN ' ON ' + T.[FileGroup] ELSE '' END +
                                  CASE WHEN ISNULL(t.[CompressionType], 'NONE') IN ('NONE', 'ROW', 'PAGE') THEN ' WITH (DATA_COMPRESSION=' + ISNULL(t.[CompressionType], 'NONE') + ')' ELSE '' END + ''');' + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''table'', ''' + T.[Schema] + '.' + T.[Name] + ''', ''created'');' AS NVARCHAR(MAX))
                           FROM (SELECT T.[Schema], T.[Name], t.[CompressionType], t.[FileGroup], T.[VariantName],
                                        ScriptColumns = STUFF((SELECT ', ' + [ColumnScript] FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND RTRIM(ISNULL([ComputedExpression], '')) = '' ORDER BY c.[ColumnName] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
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
                                        ColumnScripts = STUFF((SELECT ', ' + CAST([ColumnScript] AS NVARCHAR(MAX)) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '' ORDER BY c.[ColumnName] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
                                        ColumnCount = (SELECT COUNT(*) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''),
                                        VariantList = STUFF((SELECT ', ' + CAST(REPLACE(RTRIM(c.[VariantName]), '''', '''''') AS NVARCHAR(MAX))
                                                               FROM #Columns C WITH (NOLOCK)
                                                               WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''
                                                                 AND RTRIM(ISNULL(c.[VariantName], '')) <> '' FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
                                   FROM #Tables T WITH (NOLOCK)
                                   WHERE NewTable = 0
                                     AND EXISTS (SELECT * FROM #Columns c WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '')) T
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
      WHERE t.NewTable = 0 AND c.NewColumn = 1 AND RTRIM(ISNULL(c.[ComputedExpression], '')) = ''

  SET NOCOUNT OFF
END TRY
BEGIN CATCH
    DECLARE @v_RethrowMsg NVARCHAR(4000) = ERROR_MESSAGE();
    RAISERROR(@v_RethrowMsg, 16, 1);
END CATCH