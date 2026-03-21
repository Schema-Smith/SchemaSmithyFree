CREATE OR ALTER PROCEDURE SchemaSmith.MissingTableAndColumnQuench
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
    WHERE COLUMNPROPERTY(OBJECT_ID(c.[Schema] + '.' + c.[TableName]), c.[OldName], 'AllowsNull') IS NOT NULL
      AND COLUMNPROPERTY(OBJECT_ID(c.[Schema] + '.' + c.[TableName]), c.[ColumnName], 'AllowsNull') IS NULL
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add New Tables', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Adding new table ' + T.[Schema] + '.' + T.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'EXEC(''CREATE TABLE ' + T.[Schema] + '.' + T.[Name] + ' (' + REPLACE(ScriptColumns, '''', '''''') + ')' +
                                  CASE WHEN ISNULL(t.[CompressionType], 'NONE') IN ('NONE', 'ROW', 'PAGE') THEN ' WITH (DATA_COMPRESSION=' + ISNULL(t.[CompressionType], 'NONE') + ')' ELSE '' END + ''');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name], t.[CompressionType],
                 ScriptColumns = (SELECT STRING_AGG([ColumnScript], ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND RTRIM(ISNULL([ComputedExpression], '')) = '')
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 1) T
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add New Physical Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Adding ' + CAST(ColumnCount AS NVARCHAR(100)) + ' new columns to ' + T.[Schema] + '.' + T.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' ADD ' + ColumnScripts + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
  FROM (SELECT T.[Schema], T.[Name],
               ColumnScripts = (SELECT STRING_AGG(CAST([ColumnScript] AS NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''),
               ColumnCount = (SELECT COUNT(*) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '')
        FROM #Tables T WITH (NOLOCK)
        WHERE NewTable = 0
          AND EXISTS (SELECT * FROM #Columns c WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '')) T
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  SET NOCOUNT OFF
END TRY
BEGIN CATCH
    THROW
END CATCH
