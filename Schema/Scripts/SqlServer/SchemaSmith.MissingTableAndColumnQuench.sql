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

  RAISERROR('Handle Table Renames', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Rename ' + T.[Schema] + '.' + T.[OldName] + ' to ' + T.[Schema] + '.' + T.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC sp_rename ''' + SchemaSmith.fn_StripBracketWrapping(T.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(T.[OldName]) + ''', ''' + SchemaSmith.fn_StripBracketWrapping(T.[Name]) + ''';' + CHAR(13) + CHAR(10) AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Tables T WITH (NOLOCK)
    WHERE OBJECT_ID(T.[Schema] + '.' + T.[OldName]) IS NOT NULL
      AND OBJECT_ID(T.[Schema] + '.' + T.[Name]) IS NULL
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Handle Column Renames', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Rename ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[OldName] + ' to ' + c.[Schema] + '.' + c.[TableName] + '.' + c.[ColumnName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC sp_rename ''' + SchemaSmith.fn_StripBracketWrapping(c.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(c.[TableName]) + '.' + SchemaSmith.fn_StripBracketWrapping(c.[OldName]) + ''', ''' + SchemaSmith.fn_StripBracketWrapping(c.[ColumnName]) + ''', ''COLUMN'';' + CHAR(13) + CHAR(10) AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Columns c WITH (NOLOCK)
    WHERE COLUMNPROPERTY(OBJECT_ID(c.[Schema] + '.' + c.[TableName]), SchemaSmith.fn_StripBracketWrapping(c.[OldName]), 'AllowsNull') IS NOT NULL
      AND COLUMNPROPERTY(OBJECT_ID(c.[Schema] + '.' + c.[TableName]), SchemaSmith.fn_StripBracketWrapping(c.[ColumnName]), 'AllowsNull') IS NULL
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  IF OBJECT_ID('SchemaSmith.CustomTableRestore') IS NOT NULL
  BEGIN
    RAISERROR('Attempt custom table restore for tables being added in case they were custom dropped previously', 10, 100) WITH NOWAIT
    SELECT @v_SQL = STRING_AGG(CAST('EXEC SchemaSmith.CustomTableRestore ''' + SchemaSmith.fn_StripBracketWrapping(T.[Schema]) + ''', ''' + SchemaSmith.fn_StripBracketWrapping(T.[Name]) + ''';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
      FROM #Tables T WITH (NOLOCK)
      WHERE NewTable = 1
    IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

    SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  ' + T.[Schema] + '.' + T.[Name] + ' Restored'', 10, 100) WITH NOWAIT;' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
      FROM #Tables T WITH (NOLOCK)
      WHERE NewTable = 1
        AND OBJECT_ID([Schema] + '.' + [Name]) IS NOT NULL
    IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

    UPDATE #Tables
      SET NewTable = 0
      WHERE NewTable = 1
        AND OBJECT_ID([Schema] + '.' + [Name]) IS NOT NULL
  END

  RAISERROR('Add New Tables', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Adding new table ' + T.[Schema] + '.' + T.[Name] +
                                  CASE WHEN RTRIM(ISNULL(T.[VariantName], '')) <> '' THEN ' (variant: ' + REPLACE(RTRIM(T.[VariantName]), '''', '''''') + ')' ELSE '' END + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC(''CREATE TABLE ' + T.[Schema] + '.' + T.[Name] + ' (' + REPLACE(ScriptColumns, '''', '''''') + ')' +
                                  CASE WHEN ISNULL(t.[CompressionType], 'NONE') IN ('NONE', 'ROW', 'PAGE') THEN ' WITH (DATA_COMPRESSION=' + ISNULL(t.[CompressionType], 'NONE') + ')' ELSE '' END + ''');' + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''table'', ''' + T.[Schema] + '.' + T.[Name] + ''', ''created'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name], t.[CompressionType], T.[VariantName],
                 ScriptColumns = STUFF((SELECT ', ' + [ColumnScript] FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND RTRIM(ISNULL([ComputedExpression], '')) = '' ORDER BY c.[ColumnName] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, '')
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 1) T
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  -- Object-change audit (#363): WhatIf twin of the embedded 'table'/'created' row above. That row
  -- rides the CREATE TABLE DDL (executed only on a real run); under WhatIf the DDL is printed, so
  -- capture the would-create here from the same #Tables state.
  IF @WhatIf = 1
    INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType)
      SELECT @@SPID, 'table', T.[Schema] + '.' + T.[Name], 'wouldCreate'
        FROM #Tables T WITH (NOLOCK) WHERE NewTable = 1

  RAISERROR('Add New Physical Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Adding ' + CAST(ColumnCount AS NVARCHAR(100)) + ' new columns to ' + T.[Schema] + '.' + T.[Name] +
                                  CASE WHEN RTRIM(ISNULL(VariantList, '')) <> '' THEN ' (variant: ' + VariantList + ')' ELSE '' END + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' ADD ' + ColumnScripts + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
  FROM (SELECT T.[Schema], T.[Name],
               ColumnScripts = STUFF((SELECT ', ' + CAST([ColumnScript] AS NVARCHAR(MAX)) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '' ORDER BY c.[ColumnName] FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 2, ''),
               ColumnCount = (SELECT COUNT(*) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''),
               VariantList = (SELECT STRING_AGG(CAST(REPLACE(RTRIM(c.[VariantName]), '''', '''''') AS NVARCHAR(MAX)), ', ')
                                FROM #Columns C WITH (NOLOCK)
                                WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''
                                  AND RTRIM(ISNULL(c.[VariantName], '')) <> '')
        FROM #Tables T WITH (NOLOCK)
        WHERE NewTable = 0
          AND EXISTS (SELECT * FROM #Columns c WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '')) T
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