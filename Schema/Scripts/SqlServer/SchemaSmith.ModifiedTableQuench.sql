-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR ALTER PROCEDURE SchemaSmith.ModifiedTableQuench
  @ProductName NVARCHAR(50),
  @WhatIf BIT = 0,
  @DropUnknownIndexes BIT = 0,
  @DropTablesRemovedFromProduct BIT = 1,
  @DropColumnsRemovedFromProduct BIT = 1,
  @DropForeignKeysRemovedFromProduct BIT = 1,
  @DropCheckConstraintsRemovedFromProduct BIT = 1,
  @DropExcludeConstraintsRemovedFromProduct BIT = 1,
  @DropStatisticsRemovedFromProduct BIT = 1,
  @DropIndexesRemovedFromProduct BIT = 1,
  @CaptureWouldDrop BIT = 0
AS
BEGIN TRY
  DECLARE @v_SQL NVARCHAR(MAX) = '',
          @v_DatabaseCollation NVARCHAR(200) = CAST(DATABASEPROPERTYEX(DB_NAME(), 'COLLATION') AS NVARCHAR(200))
  SET NOCOUNT ON
  RAISERROR('Override table compression to match clustered index', 10, 100) WITH NOWAIT
  UPDATE t
    SET [CompressionType] = CASE WHEN [ColumnStore] = 1 THEN 'COLUMNSTORE' ELSE i.[CompressionType] END
    FROM #Tables t
    JOIN #Indexes i WITH (NOLOCK) ON i.[Schema] = t.[Schema]
                                 AND i.[TableName] = t.[Name]
                                 AND i.[Clustered] = 1
 
  RAISERROR('Get Schema List', 10, 100) WITH NOWAIT
  SELECT DISTINCT t.[Schema]
    INTO #SchemaList
    FROM #Tables t WITH (NOLOCK)

  RAISERROR('Turn off Temporal Tracking for tables no longer defined temporal', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Turn OFF Temporal Tracking for ' + T.[Schema] + '.' + T.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' SET (SYSTEM_VERSIONING = OFF);' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' DROP PERIOD FOR SYSTEM_TIME;' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Tables T WITH (NOLOCK)
    WHERE t.IsTemporal = 0
      AND OBJECTPROPERTY(OBJECT_ID([Schema] + '.' + [Name]), 'TableTemporalType') = 2
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect table level extended properties', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #TableProperties
  SELECT [Schema], objname COLLATE DATABASE_DEFAULT AS TableName, x.[Name] COLLATE DATABASE_DEFAULT AS PropertyName, CONVERT(NVARCHAR(50), x.[value]) COLLATE DATABASE_DEFAULT AS [value]
    INTO #TableProperties
    FROM #SchemaList WITH (NOLOCK)
    CROSS APPLY fn_listextendedproperty(default, 'Schema', SchemaSmith.fn_StripBracketWrapping([Schema]), 'Table', default, default, default) x
    WHERE x.[Name] COLLATE DATABASE_DEFAULT IN ('ProductName', 'PreventDrop')

  RAISERROR('Validate Table Ownership', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Table ' + tp.[Schema] + '.' + tp.[TableName] + ' owned by different product. [' + tp.[Value] + ']'', 10, 100) WITH NOWAIT;' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Tables t WITH (NOLOCK)
    JOIN #TableProperties tp WITH (NOLOCK) ON t.[Schema] = tp.[Schema]
                                          AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName
    WHERE tp.PropertyName = 'ProductName'
      AND tp.[value] <> @ProductName
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  IF EXISTS (SELECT *
               FROM #Tables t WITH (NOLOCK)
               JOIN #TableProperties tp WITH (NOLOCK) ON t.[Schema] = tp.[Schema]
                                                     AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName
               WHERE tp.PropertyName = 'ProductName'
                 AND tp.[value] <> @ProductName)
  BEGIN
    RAISERROR('One or more tables in this quench are already owned by another product', 16, 1) WITH NOWAIT
  END
  
  -- No-drop protection tier (#270): when protected mode is active the caller forces
  -- @DropTablesRemovedFromProduct to 0 so the drop block below never runs. Record the tables that
  -- WOULD have been dropped by absence (owned by this product, absent from the package, not already
  -- sticky-PreventDrop) to the ChangeAudit seam as 'wouldDrop' so the run can surface a manifest.
  -- Audit rows only -- no DDL -- so this runs regardless of @WhatIf.
  IF @CaptureWouldDrop = 1
  BEGIN
    RAISERROR('Capture tables suppressed by PreventDrop (would drop by absence)', 10, 100) WITH NOWAIT
    SELECT @v_SQL = STRING_AGG(CAST(
      'RAISERROR(''  Table ' + tp.[Schema] + '.' + tp.TableName + ' removed from product but PreventDrop is active -- skipping drop (protected)'', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
      'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''table'', ''' + tp.[Schema] + '.[' + tp.TableName + ']'', ''wouldDrop'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
      FROM #TableProperties tp
      WHERE tp.PropertyName = 'ProductName'
        AND tp.[value] = @ProductName
        AND NOT EXISTS (SELECT 1
                          FROM #TableProperties px WITH (NOLOCK)
                          WHERE px.[Schema] = tp.[Schema]
                            AND px.TableName = tp.TableName
                            AND px.PropertyName = 'PreventDrop'
                            AND px.[value] = 'true')
        AND NOT EXISTS (SELECT *
                          FROM #Tables t WITH (NOLOCK)
                          WHERE t.[Schema] = tp.[Schema]
                            AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName)
    IF @v_SQL IS NOT NULL EXEC(@v_SQL)
  END

  IF @DropTablesRemovedFromProduct = 1
  BEGIN
    RAISERROR('Identify tables removed from the product', 10, 100) WITH NOWAIT
    DROP TABLE IF EXISTS #TablesRemovedFromProduct
    SELECT tp.[Schema], tp.TableName
      INTO #TablesRemovedFromProduct
      FROM #TableProperties tp
      WHERE tp.PropertyName = 'ProductName'
        AND tp.[value] = @ProductName
        AND NOT EXISTS (SELECT 1
                          FROM #TableProperties px WITH (NOLOCK)
                          WHERE px.[Schema] = tp.[Schema]
                            AND px.TableName = tp.TableName
                            AND px.PropertyName = 'PreventDrop'
                            AND px.[value] = 'true')
        AND NOT EXISTS (SELECT *
                          FROM #Tables t WITH (NOLOCK)
                          WHERE t.[Schema] = tp.[Schema]
                            AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName)

    IF EXISTS (SELECT * FROM #TablesRemovedFromProduct WITH (NOLOCK))
    BEGIN
      -- A system-versioned temporal table can't be dropped while versioning is on (error 13552).
      -- Capture each removed temporal table's history table BEFORE turning versioning off
      -- (history_table_id is only valid while versioning is on), then turn versioning off so the
      -- table can be dropped; the now-orphaned history table is dropped after the main drop below.
      RAISERROR('Turn off system versioning for temporal tables removed from the product', 10, 100) WITH NOWAIT
      DROP TABLE IF EXISTS #RemovedTemporalHistory
      SELECT hs.[name] COLLATE DATABASE_DEFAULT AS HistSchema, h.[name] COLLATE DATABASE_DEFAULT AS HistName
        INTO #RemovedTemporalHistory
        FROM #TablesRemovedFromProduct t WITH (NOLOCK)
        JOIN sys.tables mt WITH (NOLOCK) ON mt.[object_id] = OBJECT_ID(t.[Schema] + '.[' + t.[TableName] + ']') AND mt.temporal_type = 2
        JOIN sys.tables h WITH (NOLOCK) ON h.[object_id] = mt.history_table_id
        JOIN sys.schemas hs WITH (NOLOCK) ON hs.[schema_id] = h.[schema_id]
      SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Turn OFF system versioning for ' + t.[Schema] + '.' + t.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                      'ALTER TABLE ' + t.[Schema] + '.[' + t.[TableName] + '] SET (SYSTEM_VERSIONING = OFF);' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
        FROM #TablesRemovedFromProduct t WITH (NOLOCK)
        WHERE OBJECTPROPERTY(OBJECT_ID(t.[Schema] + '.[' + t.[TableName] + ']'), 'TableTemporalType') = 2
      IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

      RAISERROR('Drop inbound foreign keys referencing tables removed from the product', 10, 100) WITH NOWAIT
      SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping inbound foreign Key ' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) + '.' + fk.[name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                      'ALTER TABLE [' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '].[' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + fk.[name] + '];' + CHAR(13) + CHAR(10) +
                                      'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''foreignKey'', ''[' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '].[' + OBJECT_NAME(fk.parent_object_id) + '].[' + fk.[name] + ']'', ''dropped'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
        FROM #TablesRemovedFromProduct t WITH (NOLOCK)
        JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.referenced_object_id = OBJECT_ID(t.[Schema] + '.[' + t.[TableName] + ']')
      IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

      RAISERROR('Drop tables removed from the product', 10, 100) WITH NOWAIT
      SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping table ' + t.[Schema] + '.' + t.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                      CASE WHEN OBJECT_ID('SchemaSmith.CustomTableDrop') IS NOT NULL
                                           THEN 'EXEC SchemaSmith.CustomTableDrop ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', ''' + t.[TableName] + ''';'
                                           ELSE 'DROP TABLE IF EXISTS ' + t.[Schema] + '.[' + t.[TableName] + '];'
                                           END + CHAR(13) + CHAR(10) +
                                      'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''table'', ''' + t.[Schema] + '.[' + t.[TableName] + ']'', ''dropped'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
        FROM #TablesRemovedFromProduct t WITH (NOLOCK)
      IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

      -- Drop the now-orphaned history tables of removed temporal tables (versioning was turned off
      -- above). Skipped when a CustomTableDrop hook is installed -- that hook owns table removal,
      -- including any history handling for the recycle path.
      IF OBJECT_ID('SchemaSmith.CustomTableDrop') IS NULL
      BEGIN
        RAISERROR('Drop history tables of temporal tables removed from the product', 10, 100) WITH NOWAIT
        SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping history table ' + h.HistSchema + '.' + h.HistName + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                        'DROP TABLE IF EXISTS [' + h.HistSchema + '].[' + h.HistName + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
          FROM #RemovedTemporalHistory h WITH (NOLOCK)
        IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
      END
    END
  END

  RAISERROR('Report tables removed from the product but retained by PreventDrop', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Retaining table ' + tp.[Schema] + '.' + tp.TableName + ' - removed from product but protected by PreventDrop'', 10, 100) WITH NOWAIT;' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #TableProperties tp WITH (NOLOCK)
    WHERE tp.PropertyName = 'ProductName'
      AND tp.[value] = @ProductName
      AND EXISTS (SELECT 1
                    FROM #TableProperties px WITH (NOLOCK)
                    WHERE px.[Schema] = tp.[Schema]
                      AND px.TableName = tp.TableName
                      AND px.PropertyName = 'PreventDrop'
                      AND px.[value] = 'true')
      AND NOT EXISTS (SELECT *
                        FROM #Tables t WITH (NOLOCK)
                        WHERE t.[Schema] = tp.[Schema]
                          AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Collect index level extended properties', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexProperties
  SELECT t.[Schema], t.[Name] AS TableName, objname COLLATE DATABASE_DEFAULT AS IndexName, x.[Name] COLLATE DATABASE_DEFAULT AS PropertyName, CONVERT(NVARCHAR(50), x.[value]) COLLATE DATABASE_DEFAULT AS [value]
    INTO #IndexProperties
    FROM #Tables t WITH (NOLOCK)
    CROSS APPLY fn_listextendedproperty(default, 'Schema', SchemaSmith.fn_StripBracketWrapping(t.[Schema]), 'Table', SchemaSmith.fn_StripBracketWrapping(t.[Name]), 'Index', default) x
    WHERE x.[Name] COLLATE DATABASE_DEFAULT = 'ProductName'

  RAISERROR('Identify indexes removed from the product', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesRemovedFromProduct
  SELECT xp.[Schema], xp.TableName, xp.IndexName, IsConstraint = CAST(CASE WHEN OBJECT_ID(xp.[Schema] + '.' + xp.IndexName) IS NOT NULL THEN 1 ELSE 0 END AS BIT)
    INTO #IndexesRemovedFromProduct
    FROM #IndexProperties xp
    WHERE xp.[value] = @ProductName
      AND NOT EXISTS (SELECT * 
                        FROM #Indexes i WITH (NOLOCK) 
                        WHERE i.[Schema] = xp.[Schema] 
                          AND i.TableName = xp.TableName
                          AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = xp.IndexName)
      AND NOT EXISTS (SELECT * 
                        FROM #XmlIndexes i WITH (NOLOCK) 
                        WHERE i.[Schema] = xp.[Schema] 
                          AND i.TableName = xp.TableName
                          AND SchemaSmith.fn_StripBracketWrapping(i.IndexName) = xp.IndexName)

  RAISERROR('Detect Column Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ColumnChanges
  SELECT c.[Schema], c.[TableName], c.[ColumnName],
         -- For computed columns, only the expression is needed
         CASE WHEN RTRIM(ISNULL([ComputedExpression], '')) <> ''
              THEN 'AS (' + ComputedExpression + ')' + CASE WHEN c.[Persisted] = 1 THEN ' PERSISTED' ELSE '' END
                                                    + CASE WHEN c.[Persisted] = 1 AND ISNULL(c.[Nullable], 1) = 0 THEN ' NOT NULL' ELSE '' END
              -- Otherwise we need to build the column definition
              ELSE REPLACE(REPLACE(UPPER(LEFT([DataType], COALESCE(NULLIF(CHARINDEX('IDENTITY', [DataType]), 0), LEN([DataType]) + 1) - 1)), 'ROWGUIDCOL', ''), 'NOT FOR REPLICATION', '') +
                   CASE WHEN [Collation] <> 'IGNORE' AND ISNULL(NULLIF(ic.COLLATION_NAME, @v_DatabaseCollation), '') <> [Collation] THEN ' COLLATE ' + ISNULL(NULLIF(RTRIM([Collation]), ''), @v_DatabaseCollation) ELSE '' END +
                   CASE WHEN [Sparse] = 1 THEN ' SPARSE' ELSE '' END +
                   CASE WHEN Nullable = 1 THEN ' NULL' ELSE ' NOT NULL' END +
                   CASE WHEN RTRIM(ISNULL([EncryptionType], 'NONE')) <> 'NONE'
                        THEN ' ENCRYPTED WITH (COLUMN_ENCRYPTION_KEY = ' + [EncryptionKey] + ', ENCRYPTION_TYPE = ' + [EncryptionType] + ', ALGORITHM = ''' + [EncryptionAlgorithm] + ''')'
                        ELSE '' END
              END AS [ColumnScript],
         CASE WHEN RTRIM(ISNULL([ComputedExpression], '')) = '' 
              THEN CASE WHEN [DataType] LIKE '%ROWGUIDCOL%' AND sc.is_rowguidcol = 0 THEN ' ADD ROWGUIDCOL' ELSE '' END +
                   CASE WHEN [DataType] NOT LIKE '%ROWGUIDCOL%' AND sc.is_rowguidcol = 1 THEN ' DROP ROWGUIDCOL' ELSE '' END +
                   CASE WHEN [DataType] LIKE '%NOT FOR REPLICATION%' AND ident.is_not_for_replication = 0 THEN ' ADD NOT FOR REPLICATION' ELSE '' END +
                   CASE WHEN [DataType] NOT LIKE '%NOT FOR REPLICATION%' AND ident.is_not_for_replication = 1 THEN ' DROP NOT FOR REPLICATION' ELSE '' END +
                   CASE WHEN mc.masking_function IS NOT NULL AND ([DataMaskFunction] = '' OR mc.masking_function COLLATE DATABASE_DEFAULT <> [DataMaskFunction]) THEN ' DROP MASKED' ELSE '' END +
                   CASE WHEN [DataMaskFunction] <> '' AND mc.masking_function IS NULL THEN ' ADD MASKED WITH (FUNCTION = ''' + [DataMaskFunction] + ''')' ELSE '' END +
                   CASE WHEN [DataMaskFunction] <> '' AND mc.masking_function COLLATE DATABASE_DEFAULT <> [DataMaskFunction]
                        THEN '; ALTER TABLE ' + c.[Schema] + '.' + c.[TableName] + ' ALTER COLUMN ' + c.[ColumnName] + ' ADD MASKED WITH (FUNCTION = ''' + [DataMaskFunction] + ''')'
                        ELSE '' END
              ELSE ''
              END AS [SpecialColumnScript],
         CAST(CASE WHEN cc.[definition] IS NOT NULL OR RTRIM(ISNULL([ComputedExpression], '')) <> ''
                     OR (ident.column_id IS NULL AND [DataType] LIKE '%IDENTITY%') -- switching to identity... requires drop and recreate column
                   THEN 1 ELSE 0 END AS BIT) AS MustDropAndRecreate,
         CAST(CASE WHEN (ident.column_id IS NOT NULL AND [DataType] NOT LIKE '%IDENTITY%'
                        AND RTRIM(ISNULL([ComputedExpression], '')) = '') -- identity removal (data-preserving swap)
                    OR (ISNULL(sc.encryption_type_desc, 'NONE') COLLATE DATABASE_DEFAULT <> [EncryptionType]) -- encryption change (data-preserving swap)
                   THEN 1 ELSE 0 END AS BIT) AS MustSwapColumn,
         CAST(0 AS BIT) AS DropOnly
    INTO #ColumnChanges
    FROM #Tables T WITH (NOLOCK)
    JOIN #Columns c WITH (NOLOCK) ON C.[Schema] = T.[Schema] 
                                 AND C.[TableName] = T.[Name]
                                 AND C.[NewColumn] = 0
    JOIN INFORMATION_SCHEMA.COLUMNS ic  WITH (NOLOCK) ON ic.TABLE_SCHEMA = SchemaSmith.fn_StripBracketWrapping(C.[Schema])
                                                     AND ic.TABLE_NAME = SchemaSmith.fn_StripBracketWrapping(C.[TableName])
                                                     AND ic.COLUMN_NAME = SchemaSmith.fn_StripBracketWrapping(C.[ColumnName])
    JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(ic.TABLE_SCHEMA + '.' + ic.TABLE_NAME) AND sc.[name] = ic.COLUMN_NAME
    JOIN (SELECT CASE WHEN SCHEMA_NAME(st.[schema_id]) IN ('sys', 'dbo')
                      THEN '' ELSE SCHEMA_NAME(st.[schema_id]) + '.' END + st.[name] AS USER_TYPE, st.user_type_id
            FROM sys.types st WITH (NOLOCK)) st ON st.user_type_id = sc.user_type_id
    LEFT JOIN sys.identity_columns ident WITH (NOLOCK) ON ident.[Name] = COLUMN_NAME
                                                      AND ident.[object_id] = OBJECT_ID(TABLE_SCHEMA + '.' + TABLE_NAME)
    LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = SchemaSmith.fn_StripBracketWrapping(c.ColumnName)
                                                   AND cc.[object_id] = OBJECT_ID(C.[Schema] + '.' + C.[TableName])
    LEFT JOIN sys.masked_columns mc WITH (NOLOCK) ON mc.[name] = SchemaSmith.fn_StripBracketWrapping(c.ColumnName)
                                                 AND mc.[object_id] = OBJECT_ID(C.[Schema] + '.' + C.[TableName])
    WHERE t.NewTable = 0
      AND (REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(UPPER(USER_TYPE) + CASE WHEN USER_TYPE LIKE '%CHAR' OR USER_TYPE LIKE '%BINARY'
                                           THEN '(' + CASE WHEN CHARACTER_MAXIMUM_LENGTH = -1 THEN 'MAX' ELSE CONVERT(NVARCHAR(20), CHARACTER_MAXIMUM_LENGTH) END + ')'
                                           WHEN USER_TYPE IN ('NUMERIC', 'DECIMAL')
                                           THEN  '(' + CONVERT(NVARCHAR(20), NUMERIC_PRECISION) + ', ' + CONVERT(NVARCHAR(20), NUMERIC_SCALE) + ')'
                                           WHEN USER_TYPE = 'DATETIME2'
                                           THEN  '(' + CONVERT(NVARCHAR(20), DATETIME_PRECISION) + ')'
                                           WHEN USER_TYPE = 'XML' AND sc.xml_collection_id <> 0
                                           THEN  '(' + (SELECT '[' + SCHEMA_NAME(xc.[schema_id]) + '].[' + xc.[name] + ']' FROM sys.xml_schema_collections xc WHERE xc.xml_collection_id = sc.xml_collection_id) + ')'
                                           WHEN USER_TYPE = 'UNIQUEIDENTIFIER' AND sc.is_rowguidcol = 1
                                           THEN  ' ROWGUIDCOL'
                                           ELSE '' END +
                                      CASE WHEN ident.column_id IS NOT NULL
                                           THEN ' IDENTITY(' + CONVERT(NVARCHAR(20), ident.seed_value) + ', ' + CONVERT(NVARCHAR(20), ident.increment_value) + ')' +
                                                CASE WHEN ident.is_not_for_replication = 1 THEN ' NOT FOR REPLICATION' ELSE '' END
                                           ELSE '' END), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')  <> REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(REPLACE(UPPER(c.DataType), ' (', '('), '( ', '('), ' )', ')'), ', ', ','), ' ,', ','), 'DECIMAL', 'NUMERIC')
        OR CASE WHEN c.Nullable = 1 THEN 'YES' ELSE 'NO' END <> ic.IS_NULLABLE
        OR ISNULL(SchemaSmith.fn_StripParenWrapping(cc.[definition]), '') <> ISNULL(c.ComputedExpression, '')
        OR ISNULL(cc.is_persisted, 0) <> ISNULL(c.[Persisted], 0))
        OR sc.is_sparse <> [Sparse]
        OR ISNULL(mc.masking_function, '') COLLATE DATABASE_DEFAULT <> [DataMaskFunction]
        OR ([Collation] <> 'IGNORE' AND ISNULL(NULLIF(ic.COLLATION_NAME, @v_DatabaseCollation), '') <> [Collation])
        OR ISNULL(sc.encryption_type_desc, 'NONE') COLLATE DATABASE_DEFAULT <> [EncryptionType]
        OR (ISNULL(sc.encryption_type_desc, 'NONE') COLLATE DATABASE_DEFAULT <> 'NONE' AND (ISNULL(sc.encryption_algorithm_name, '') COLLATE DATABASE_DEFAULT <> [EncryptionAlgorithm] OR ISNULL(sc.column_encryption_key_database_name, '') COLLATE DATABASE_DEFAULT <> [EncryptionKey]))
  
  RAISERROR('Detect Computed Columns Impacted by Other Column Changes', 10, 100) WITH NOWAIT
  INSERT #ColumnChanges ([Schema], [TableName], [ColumnName], [ColumnScript], [SpecialColumnScript], MustDropAndRecreate, MustSwapColumn, [DropOnly])
    SELECT C.[Schema], C.[TableName], c.[ColumnName],
           [ColumnScript] = 'AS (' + ComputedExpression + ')' + CASE WHEN c.[Persisted] = 1 THEN ' PERSISTED' ELSE '' END
                                                              + CASE WHEN c.[Persisted] = 1 AND ISNULL(c.[Nullable], 1) = 0 THEN ' NOT NULL' ELSE '' END,
           [SpecialColumnScript] = '',
           MustDropAndRecreate = CAST(1 AS BIT), MustSwapColumn = CAST(0 AS BIT), [DropOnly] = CAST(0 AS BIT)
      FROM #ColumnChanges cc WITH (NOLOCK)
      JOIN sys.computed_columns sc WITH (NOLOCK) ON sc.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName])
                                                AND sc.[definition] LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'
      JOIN #Columns c WITH (NOLOCK) ON C.[Schema] = cc.[Schema] 
                                   AND C.[TableName] = cc.[TableName]
                                   AND c.[ColumnName] = cc.[ColumnName]
      WHERE NOT EXISTS (SELECT * FROM #ColumnChanges cc2 WITH (NOLOCK) WHERE cc2.[Schema] = cc.[Schema] AND cc2.[TableName] = cc.[TableName] AND cc2.[ColumnName] = cc.[ColumnName])
  
  RAISERROR('Detect Column Drops', 10, 100) WITH NOWAIT
  INSERT #ColumnChanges ([Schema], [TableName], [ColumnName], [ColumnScript], [SpecialColumnScript], MustDropAndRecreate, MustSwapColumn, [DropOnly])
    SELECT t.[Schema], [TableName] = t.[Name], [ColumnName] = '[' + COLUMN_NAME + ']', '', '', 0, 0, 1
      FROM #Tables t WITH (NOLOCK)
      JOIN INFORMATION_SCHEMA.COLUMNS WITH (NOLOCK) ON TABLE_SCHEMA = SchemaSmith.fn_StripBracketWrapping(t.[Schema])
                                                   AND TABLE_NAME = SchemaSmith.fn_StripBracketWrapping(t.[Name]) 
      WHERE NOT EXISTS (SELECT * 
                          FROM #Columns c WITH (NOLOCK)
                          WHERE c.[Schema] = t.[Schema]
                            AND c.[TableName] = t.[Name]
                            AND SchemaSmith.fn_StripBracketWrapping(c.[ColumnName]) = COLUMN_NAME)
        AND NOT (t.IsTemporal = 1 AND COLUMN_NAME IN ('ValidFrom', 'ValidTo'))
  
  RAISERROR('Collect Foreign Keys To Drop', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #FKsToDrop
  SELECT t.[Schema], [TableName] = t.[Name], [FKName] = fk.[Name]
    INTO #FKsToDrop
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.parent_object_id = OBJECT_ID(t.[Schema] + '.' + t.[Name])
    WHERE NOT EXISTS (SELECT * FROM #ForeignKeys fk2 WITH (NOLOCK) WHERE t.[Schema] = fk2.[Schema] AND t.[Name] = fk2.[TableName] AND fk.[name] = SchemaSmith.fn_StripBracketWrapping(fk2.[KeyName]))
      AND @DropForeignKeysRemovedFromProduct = 1
      AND ISNULL(t.[DropForeignKeysRemovedFromProduct], 1) = 1

  RAISERROR('Drop Foreign Keys No Longer Defined In The Product', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping foreign Key ' + df.[Schema] + '.' + df.[TableName] + '.' + df.[FKName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + df.[Schema] + '.' + df.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + df.[FKName] + '];' + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''foreignKey'', ''' + df.[Schema] + '.' + df.[TableName] + '.' + df.[FKName] + ''', ''dropped'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
        FROM #FKsToDrop df WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Fulltext Indexes To Drop Based On Column Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #FTIndexesToDropForChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName]
    INTO #FTIndexesToDropForChanges
    FROM sys.fulltext_index_columns ic WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON ic.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
                                        AND COL_NAME(ic.[object_id], ic.column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
  
  RAISERROR('Drop FullText Indexes Referencing Modified Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping fulltext index on ' + di.[Schema] + '.' + di.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP FULLTEXT INDEX ON ' + di.[Schema] + '.' + di.[TableName] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #FTIndexesToDropForChanges di WITH (NOLOCK)
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(di.[Schema] + '.' + di.[TableName])
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing FullText Indexes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingFullTextIndexes
  SELECT t.[Schema], [TableName] = t.[Name],
         (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[object_id], fc.column_id) + ']' +
                            CASE WHEN fc.type_column_id IS NOT NULL
                                 THEN ' TYPE COLUMN [' + COL_NAME(fc.[object_id], fc.type_column_id) + ']'
                                 ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(fc.[object_id], fc.column_id))
            FROM sys.fulltext_index_columns fc WITH (NOLOCK)
            WHERE fi.[object_id] = fc.[object_id]) AS [Columns],
         FullTextCatalog = '[' + (SELECT c.[name] COLLATE DATABASE_DEFAULT FROM sys.fulltext_catalogs c WITH (NOLOCK) WHERE c.fulltext_catalog_id = fi.fulltext_catalog_id) + ']',
         KeyIndex = '[' + (SELECT i.[Name] COLLATE DATABASE_DEFAULT FROM sys.indexes i WITH (NOLOCK) WHERE i.[object_id] = fi.[object_id] AND i.[index_id] = fi.[unique_index_id]) + ']',
         ChangeTracking = change_tracking_state_desc COLLATE DATABASE_DEFAULT,
         [StopList] = '[' + COALESCE((SELECT fs.[name] COLLATE DATABASE_DEFAULT FROM sys.fulltext_stoplists fs WITH (NOLOCK) WHERE fs.stoplist_id = fi.stoplist_id), 'SYSTEM') + ']'
    INTO #ExistingFullTextIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
    WHERE t.NewTable = 0
  
  RAISERROR('Identify Indexes To Drop Based On Column Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesToDropForColumnChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName], IndexName = i.[name],
         IsConstraint = CAST(CASE WHEN i.is_primary_key = 1 OR i.is_unique_constraint = 1 THEN 1 ELSE 0 END AS BIT),
         IsUnique = i.is_unique,
         IsClustered = CAST(CASE WHEN i.[type_desc] = 'CLUSTERED' THEN 1 ELSE 0 END AS BIT)
    INTO #IndexesToDropForColumnChanges
    FROM sys.indexes i WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON i.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
    LEFT JOIN sys.index_columns ic WITH (NOLOCK) ON ic.[object_id] = i.[object_id]
                                                AND ic.[index_id] = i.[index_id]
                                                AND COL_NAME(ic.[object_id], ic.column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
    WHERE ic.column_id IS NOT NULL
       OR i.filter_definition LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'
  
  -- Handle table compression changes
  RAISERROR('Fixup Table Compression', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Altering table compression for ' + t.[Schema] + '.' + t.[Name] + ' TO ' + t.[CompressionType] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + t.[Schema] + '.' + t.[Name] + ' REBUILD PARTITION=ALL WITH (DATA_COMPRESSION=' + t.[CompressionType] + ');' + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''table'', ''' + t.[Schema] + '.' + t.[Name] + ''', ''modified'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Tables t WITH (NOLOCK)
    LEFT JOIN sys.partitions AS p WITH (NOLOCK) ON p.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
                                               AND p.index_id < 2
    WHERE t.NewTable = 0
      AND t.[CompressionType] IN ('NONE', 'ROW', 'PAGE')
      AND COALESCE(p.data_compression_desc COLLATE DATABASE_DEFAULT, 'NONE') <> t.[CompressionType]
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  -- Handle index compression changes
  RAISERROR('Fixup Index Compression', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Altering index compression for ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ' TO ' + i.[CompressionType] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' REBUILD PARTITION=ALL WITH (DATA_COMPRESSION=' + i.[CompressionType] + ');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Indexes i WITH (NOLOCK) 
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName])
                                     AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    LEFT JOIN sys.partitions p WITH (NOLOCK) ON p.[object_id] = si.[object_id]
                                            AND p.index_id = si.index_id
    WHERE COALESCE(p.data_compression_desc COLLATE DATABASE_DEFAULT, 'NONE') <> i.[CompressionType]
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Collect Existing Index Definitions', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingIndexes
  SELECT xSchema = t.[Schema], [xTableName] = t.[Name], [xIndexName] = CAST(si.[Name] AS NVARCHAR(500)),
         IsConstraint = CAST(CASE WHEN si.is_primary_key = 1 OR si.is_unique_constraint = 1 THEN 1 ELSE 0 END AS BIT),
         IsUnique = si.is_unique, IsClustered = CAST(CASE WHEN si.[type_desc] = 'CLUSTERED' THEN 1 ELSE 0 END AS BIT), [FillFactor] = ISNULL(NULLIF(si.fill_factor, 0), 100),
         IndexScript = 'CREATE ' + 
                       CASE WHEN si.is_unique = 1 THEN 'UNIQUE ' ELSE '' END + 
                       CASE WHEN si.[type] IN (1, 5) THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                       CASE WHEN si.[type] IN (5, 6) THEN 'COLUMNSTORE ' ELSE '' END + 
                       'INDEX [' + si.[Name] + '] ON ' + t.[Schema] + '.' + t.[Name] + 
                       CASE WHEN si.[type] NOT IN (5, 6) 
                            THEN ' (' + (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY key_ordinal)
                                           FROM sys.index_columns ic WITH (NOLOCK)
                                           WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 0) + ')' +
                                 CASE WHEN EXISTS (SELECT * FROM sys.index_columns ic WITH (NOLOCK) WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1)
                                      THEN ' INCLUDE (' +
                                           (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(ic.[object_id], ic.column_id))
                                              FROM sys.index_columns ic WITH (NOLOCK)
                                              WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) + ')'
                                      ELSE '' END
                            WHEN si.[type] IN (6) 
                            THEN ' (' + (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(ic.[object_id], ic.column_id))
                                           FROM sys.index_columns ic WITH (NOLOCK)
                                           WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) + ')'
                            ELSE '' END +
                       CASE WHEN si.has_filter = 1 THEN ' WHERE ' + SchemaSmith.fn_StripParenWrapping(si.filter_definition) ELSE '' END +
                       CASE WHEN (si.[type] NOT IN (5, 6) AND ISNULL(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT IN ('NONE', 'ROW', 'PAGE'))
                              OR (si.[type] IN (5, 6) AND ISNULL(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                            THEN ' WITH (DATA_COMPRESSION=' + ISNULL(p.[data_compression_desc], 'NONE') COLLATE DATABASE_DEFAULT + ')'
                            ELSE '' END
    INTO #ExistingIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
                                     AND si.index_id > 0
                                     AND is_hypothetical = 0
                                     AND is_disabled = 0
    LEFT JOIN sys.partitions p WITH (NOLOCK) ON p.[object_id] = si.[object_id]
                                            AND p.index_id = si.index_id
    WHERE t.NewTable = 0
      AND NOT EXISTS (SELECT * FROM sys.xml_indexes xi WHERE xi.[object_id] = si.[object_id] AND xi.index_id = si.index_id)
    
  RAISERROR('Detect Index Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexChanges
  SELECT i.[Schema], i.[TableName], i.[IndexName], ei.[IsConstraint], IsUnique = i.[Unique], IsClustered = i.[Clustered]
    INTO #IndexChanges
    FROM #ExistingIndexes ei WITH (NOLOCK)
    JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                 AND ei.[xTableName] = i.[TableName]
                                 AND ei.[xIndexName] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE EXISTS (SELECT * 
                    FROM sys.indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND ei.IndexScript <> 'CREATE ' + 
                            CASE WHEN i.[Unique] = 1 THEN 'UNIQUE ' ELSE '' END + 
                            CASE WHEN i.[Clustered] = 1 THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                            CASE WHEN i.[ColumnStore] = 1 THEN 'COLUMNSTORE ' ELSE '' END + 
	                        'INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] +  
                            CASE WHEN i.[ColumnStore] = 0 THEN ' (' + i.[IndexColumns] + ')' + CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END
                                 WHEN i.[ColumnStore] = 1 AND i.[Clustered] = 0 THEN ' (' + i.[IncludeColumns] + ')'
                                 ELSE '' END +
                            CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
                            CASE WHEN (i.[ColumnStore] = 0 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE'))
                                   OR (i.[ColumnStore] = 1 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                                 THEN ' WITH (DATA_COMPRESSION=' + RTRIM(ISNULL(i.[CompressionType], '')) + ')'
                                 ELSE '' END
  
  RAISERROR('Detect Index Renames', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexRenames
  SELECT i.[Schema], i.[TableName], [NewName] = i.[IndexName], ei.[IsConstraint], IsUnique = i.[Unique], [OldName] = ei.[xIndexName]
    INTO #IndexRenames
    FROM #ExistingIndexes ei WITH (NOLOCK)
    JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                 AND ei.[xTableName] = i.[TableName]
                                 AND ei.[xIndexName] <> SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE NOT EXISTS (SELECT * FROM #Indexes i2 WITH (NOLOCK) WHERE i2.[Schema] = ei.[xSchema] AND i2.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i2.[IndexName]) = ei.[xIndexName])
      AND INDEXPROPERTY(OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]), SchemaSmith.fn_StripBracketWrapping(i.[IndexName]), 'IndexID') IS NULL
      -- A PK / unique constraint and a plain index are NOT rename-equivalent even when structurally
      -- identical: renaming a plain unique index into a PK name leaves an index where the constraint
      -- should be, and the PK is then never created (#304). Only rename when constraint-ness matches.
      AND ei.[IsConstraint] = (CASE WHEN i.[PrimaryKey] = 1 OR i.[UniqueConstraint] = 1 THEN 1 ELSE 0 END)
      AND EXISTS (SELECT *
                    FROM sys.indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName])
                      AND si.[name] = ei.[xIndexName])
      AND REPLACE(ei.IndexScript, ei.[xIndexName], 'IndexName') = 'CREATE ' + 
                                                                  CASE WHEN i.[Unique] = 1 OR i.[PrimaryKey] = 1 THEN 'UNIQUE ' ELSE '' END + 
                                                                  CASE WHEN i.[Clustered] = 1 THEN '' ELSE 'NON' END + 'CLUSTERED ' +
                                                                  CASE WHEN i.[ColumnStore] = 1 THEN 'COLUMNSTORE ' ELSE '' END + 
	                                                              'INDEX [IndexName] ON ' + i.[Schema] + '.' + i.[TableName] + 
                                                                  CASE WHEN i.[ColumnStore] = 0 THEN ' (' + i.[IndexColumns] + ')' + CASE WHEN RTRIM(ISNULL(i.[IncludeColumns], '')) <> '' THEN ' INCLUDE (' + i.[IncludeColumns] + ')' ELSE '' END
                                                                       WHEN i.[ColumnStore] = 1 AND i.[Clustered] = 0 THEN ' (' + i.[IncludeColumns] + ')'
                                                                       ELSE '' END +
                                                                  CASE WHEN RTRIM(ISNULL(i.[FilterExpression], '')) <> '' THEN ' WHERE ' + i.[FilterExpression] ELSE '' END +
                                                                  CASE WHEN (i.[ColumnStore] = 0 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('NONE', 'ROW', 'PAGE'))
                                                                         OR (i.[ColumnStore] = 1 AND RTRIM(ISNULL(i.[CompressionType], '')) IN ('COLUMNSTORE', 'COLUMNSTORE_ARCHIVE'))
                                                                       THEN ' WITH (DATA_COMPRESSION=' + RTRIM(ISNULL(i.[CompressionType], '')) + ')'
                                                                       ELSE '' END

  -- Remove duplicates from the rename list
  SELECT MAX([NewName]) AS ValidNewName, [OldName] AS [OriginalName]
    INTO #IndexRenameDedupe
    FROM #IndexRenames ir WITH (NOLOCK)
    GROUP BY [OldName]  
  DELETE FROM #IndexRenames WHERE EXISTS (SELECT * FROM #IndexRenameDedupe dd WITH (NOLOCK) WHERE [OriginalName] = [OldName] AND [ValidNewName] <> [NewName])
  
  RAISERROR('Handle Renamed Indexes And Unique Constraints', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Renaming ' + [OldName] + ' to ' + [NewName] + ' ON ' + ir.[Schema] + '.' + ir.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN IsConstraint = 1
                                       THEN CASE WHEN OBJECT_ID(ir.[Schema] + '.' + ir.[NewName]) IS NULL
                                                 THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''OBJECT'';'
                                                 ELSE 'ALTER TABLE ' + ir.[Schema] + '.' + ir.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + ir.[OldName] + '];'
                                                 END
                                       ELSE CASE WHEN INDEXPROPERTY(OBJECT_ID(ir.[Schema] + '.' + ir.[TableName]), SchemaSmith.fn_StripBracketWrapping(ir.[NewName]), 'IndexID') IS NULL
                                                 THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(ir.[TableName]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''INDEX'';'
                                                 ELSE 'DROP INDEX IF EXISTS [' + ir.[OldName] + '] ON ' + ir.[Schema] + '.' + ir.[TableName] + ';'
                                                 END
                                       END AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #IndexRenames ir WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Collect Existing XML Index Definitions', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingXmlIndexes
  SELECT xSchema = t.[Schema], [xTableName] = t.[Name], [xIndexName] = CAST(i.[Name] COLLATE DATABASE_DEFAULT AS NVARCHAR(500)),
         IndexScript = 'CREATE ' + CASE WHEN i.xml_index_type = 0 THEN 'PRIMARY ' ELSE '' END + 
                       'XML INDEX [' + i.[name] COLLATE DATABASE_DEFAULT + '] ON [' + OBJECT_SCHEMA_NAME(i.[object_id]) + '].[' + OBJECT_NAME(i.[object_id]) + '] ' + 
                       '([' + COL_NAME(i.[Object_id], ic.column_id) + '])' + 
                       CASE WHEN i.xml_index_type = 1 
                            THEN ' USING XML INDEX [' + (SELECT [Name] FROM sys.xml_indexes i2 WHERE i2.[object_id] = i.[object_id] AND i2.index_id = i.using_xml_index_id) COLLATE DATABASE_DEFAULT + '] ' + 
                                 'FOR ' + i.secondary_type_desc COLLATE DATABASE_DEFAULT 
                            ELSE '' END
    INTO #ExistingXmlIndexes
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.xml_indexes i ON i.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
    JOIN sys.index_columns ic ON i.[object_id] = ic.[object_id] AND i.index_id = ic.index_id
    WHERE t.NewTable = 0

  RAISERROR('Detect Xml Index Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #XmlIndexChanges
  SELECT i.[Schema], i.[TableName], i.[IndexName]
    INTO #XmlIndexChanges
    FROM #ExistingXmlIndexes ei WITH (NOLOCK)
    JOIN #XmlIndexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                    AND ei.[xTableName] = i.[TableName]
                                    AND ei.[xIndexName] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE EXISTS (SELECT * 
                    FROM sys.xml_indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND ei.IndexScript <> 'CREATE ' + CASE WHEN i.IsPrimary = 1 THEN 'PRIMARY ' ELSE '' END + 
                            'XML INDEX ' + i.[IndexName] COLLATE DATABASE_DEFAULT + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' (' + i.[Column] + ')' + 
                            CASE WHEN i.IsPrimary = 0
                                 THEN ' USING XML INDEX ' + i.PrimaryIndex + ' FOR ' + i.SecondaryIndexType
                                 ELSE '' END
  
  RAISERROR('Detect Xml Index Renames', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #XmlIndexRenames
  SELECT i.[Schema], i.[TableName], [NewName] = i.[IndexName], [OldName] = ei.[xIndexName]
    INTO #XmlIndexRenames
    FROM #ExistingXmlIndexes ei WITH (NOLOCK)
    JOIN #XmlIndexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                    AND ei.[xTableName] = i.[TableName]
                                    AND ei.[xIndexName] <> SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE NOT EXISTS (SELECT * FROM #XmlIndexes i2 WITH (NOLOCK) WHERE i2.[Schema] = ei.[xSchema] AND i2.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i2.[IndexName]) = ei.[xIndexName])
      AND INDEXPROPERTY(OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]), SchemaSmith.fn_StripBracketWrapping(i.[IndexName]), 'IndexID') IS NULL
      AND EXISTS (SELECT * 
                    FROM sys.xml_indexes si WITH (NOLOCK)
                    WHERE si.[object_id] = OBJECT_ID(ei.[xSchema] + '.' + ei.[xTableName]) 
                      AND si.[name] = ei.[xIndexName])
      AND REPLACE(ei.IndexScript, ei.[xIndexName], 'IndexName') = 'CREATE ' + CASE WHEN i.IsPrimary = 1 THEN 'PRIMARY ' ELSE '' END + 
                                                                  'XML INDEX ' + i.[IndexName] COLLATE DATABASE_DEFAULT + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' (' + i.[Column] + ')' + 
                                                                  CASE WHEN i.IsPrimary = 0
                                                                       THEN ' USING XML INDEX ' + i.PrimaryIndex + ' FOR ' + i.SecondaryIndexType
                                                                       ELSE '' END

  RAISERROR('Handle Renamed Xml Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Renaming ' + [OldName] + ' to ' + [NewName] + ' ON ' + ir.[Schema] + '.' + ir.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN INDEXPROPERTY(OBJECT_ID(ir.[Schema] + '.' + ir.[TableName]), SchemaSmith.fn_StripBracketWrapping(ir.[NewName]), 'IndexID') IS NULL
                                       THEN 'EXEC sp_rename N''' + SchemaSmith.fn_StripBracketWrapping(ir.[Schema]) + '.' + SchemaSmith.fn_StripBracketWrapping(ir.[TableName]) + '.' + ir.[OldName] + ''', N''' + SchemaSmith.fn_StripBracketWrapping(ir.[NewName]) + ''', N''INDEX'';'
                                       ELSE 'DROP INDEX IF EXISTS [' + ir.[OldName] + '] ON ' + ir.[Schema] + '.' + ir.[TableName] + ';'
                                       END AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #XmlIndexRenames ir WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Identify unknown and modified indexes to drop', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #IndexesToDrop
  SELECT [Schema] = CAST([Schema] AS NVARCHAR(500)), [TableName] = CAST([TableName] AS NVARCHAR(500)), 
         [IndexName] = CAST(SchemaSmith.fn_StripBracketWrapping([IndexName]) AS NVARCHAR(500)), [IsConstraint], [IsUnique] = i.[is_unique], 
         [IsClustered] = CAST(CASE WHEN i.[type_desc] = 'CLUSTERED' THEN 1 ELSE 0 END AS BIT)
    INTO #IndexesToDrop
    FROM #IndexesRemovedFromProduct ir WITH (NOLOCK)
    JOIN sys.indexes i WITH (NOLOCK) ON i.[object_id] = OBJECT_ID([Schema] + '.' + [TableName]) AND i.[Name] = SchemaSmith.fn_StripBracketWrapping([IndexName])
    -- Removed-from-product (ownership-stamped) drop is gated by the cascade flag + per-table
    -- tightening. The unknown (@DropUnknownIndexes) and modified branches below are unaffected.
    WHERE @DropIndexesRemovedFromProduct = 1
      AND ISNULL((SELECT t.[DropIndexesRemovedFromProduct] FROM #Tables t WITH (NOLOCK) WHERE t.[Schema] = ir.[Schema] AND t.[Name] = ir.[TableName]), 1) = 1
  UNION
  SELECT [Schema], [TableName], SchemaSmith.fn_StripBracketWrapping([IndexName]), [IsConstraint], [IsUnique], [IsClustered]
    FROM #IndexesToDropForColumnChanges WITH (NOLOCK)
  UNION
  SELECT [xSchema], [xTableName], [xIndexName], [IsConstraint], [IsUnique], [IsClustered]
    FROM #ExistingIndexes ei WITH (NOLOCK)
    WHERE @DropUnknownIndexes = 1
      AND NOT EXISTS (SELECT * FROM #Indexes i WITH (NOLOCK) WHERE i.[Schema] = ei.[xSchema] AND i.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i.[IndexName]) = ei.[xIndexName])
  UNION
  SELECT [Schema], [TableName], SchemaSmith.fn_StripBracketWrapping([IndexName]), [IsConstraint], [IsUnique], [IsClustered]
    FROM #IndexChanges WITH (NOLOCK)
  UNION
  SELECT [xSchema], [xTableName], [xIndexName], [IsConstraint] = 0, [IsUnique] = 0, [IsClustered] = 0
    FROM #ExistingXmlIndexes ei WITH (NOLOCK)
    WHERE @DropUnknownIndexes = 1
      AND NOT EXISTS (SELECT * FROM #XmlIndexes i WITH (NOLOCK) WHERE i.[Schema] = ei.[xSchema] AND i.[TableName] = ei.[xTableName] AND SchemaSmith.fn_StripBracketWrapping(i.[IndexName]) = ei.[xIndexName])
  UNION
  SELECT [Schema], [TableName], SchemaSmith.fn_StripBracketWrapping([IndexName]), [IsConstraint] = 0, [IsUnique] = 0, [IsClustered] = 0
    FROM #XmlIndexChanges WITH (NOLOCK)

  -- Need to drop all the XML indexes if we're removing the clustered PK
  INSERT #IndexesToDrop ([Schema], [TableName], [IndexName], [IsConstraint], [IsUnique], [IsClustered])
    SELECT [xSchema], [xTableName], [xIndexName], [IsConstraint] = 0, [IsUnique] = 0, [IsClustered] = 0
      FROM #ExistingXmlIndexes ei WITH (NOLOCK)
      WHERE EXISTS (SELECT * FROM #IndexesToDrop id WITH (NOLOCK) WHERE [xSchema] = [Schema] AND [xTableName] = [TableName] AND id.[IsClustered] = 1)
        AND NOT EXISTS (SELECT * FROM #IndexesToDrop id WITH (NOLOCK) WHERE [xSchema] = [Schema] AND [xTableName] = [TableName] AND [xIndexName] = [IndexName])
  
  RAISERROR('Drop Referencing Foreign Keys When Dropping Unique Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping foreign Key ' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '.' + OBJECT_NAME(fk.parent_object_id) + '.' + fk.[name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE [' + OBJECT_SCHEMA_NAME(fk.parent_object_id) + '].[' + OBJECT_NAME(fk.parent_object_id) + '] DROP CONSTRAINT IF EXISTS [' + fk.[name] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #IndexesToDrop di WITH (NOLOCK)
    JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.referenced_object_id = OBJECT_ID(di.[Schema] + '.' + di.[TableName])
    WHERE IsConstraint = 1 OR IsUnique = 1
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop FullText Indexes Referencing Unique Indexes That Will Be Dropped', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping fulltext index on ' + ef.[Schema] + '.' + ef.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP FULLTEXT INDEX ON ' + ef.[Schema] + '.' + ef.[TableName] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #IndexesToDrop id WITH (NOLOCK)
    JOIN #ExistingFullTextIndexes ef WITH (NOLOCK) ON id.[Schema] = ef.[Schema]
                                                  AND id.[TableName] = ef.[TableName]
                                                  AND id.[IndexName] = SchemaSmith.fn_StripBracketWrapping(ef.[KeyIndex])
    JOIN sys.fulltext_indexes fi WITH (NOLOCK) ON fi.[object_id] = OBJECT_ID(ef.[Schema] + '.' + ef.[TableName])
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Unknown and Modified Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping ' + CASE WHEN IsConstraint = 1 THEN 'constraint' ELSE 'index' END + ' ' + di.[Schema] + '.' + di.[TableName] + '.' + di.[IndexName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN IsConstraint = 1
                                       THEN 'ALTER TABLE ' + di.[Schema] + '.' + di.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + di.[IndexName] + '];'
                                       ELSE 'DROP INDEX IF EXISTS [' + di.[IndexName] + '] ON ' + di.[Schema] + '.' + di.[TableName] + ';'
                                       END + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''' + CASE WHEN IsConstraint = 1 THEN 'constraint' ELSE 'index' END + ''', ''' + di.[Schema] + '.' + di.[TableName] + '.' + di.[IndexName] + ''', ''dropped'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10)) WITHIN GROUP (ORDER BY CASE WHEN [IsClustered] = 0 THEN 0 ELSE 1 END)
    FROM #IndexesToDrop di WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  
  RAISERROR('Fixup Modified Fillfactors', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Fixup ' + CASE WHEN IsConstraint = 1 THEN 'constraint' ELSE 'index' END + ' fillfactor in ' + i.[Schema] + '.' + i.[TableName] + '.' + i.[IndexName] + ''', 10, 100) WITH NOWAIT; ' + 
                                  'ALTER INDEX ' + i.[IndexName] + ' ON ' + i.[Schema] + '.' + i.[TableName] + ' REBUILD WITH (FILLFACTOR = ' + CONVERT(NVARCHAR(5), i.[FillFactor]) + ', SORT_IN_TEMPDB = ON);' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ExistingIndexes ei WITH (NOLOCK)
    JOIN #Indexes i WITH (NOLOCK) ON ei.[xSchema] = i.[Schema]
                                 AND ei.[xTableName] = i.[TableName]
                                 AND ei.[xIndexName] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName])
    WHERE i.[UpdateFillFactor] = 1
      AND ei.[FillFactor] <> i.[FillFactor]
      AND INDEXPROPERTY(OBJECT_ID(i.[Schema] + '.' + i.[TableName]), ei.[xIndexName], 'IndexID') IS NOT NULL
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Statistics To Drop Based On Column Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #StatisticsToDropForChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName], [StatName] = i.[name]
    INTO #StatisticsToDropForChanges
    FROM sys.stats i WITH (NOLOCK) 
    JOIN #ColumnChanges cc WITH (NOLOCK) ON i.[object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
    LEFT JOIN sys.stats_columns ic WITH (NOLOCK) ON ic.[object_id] = i.[object_id]
                                                AND ic.[stats_id] = i.[stats_id]
                                                AND COL_NAME(ic.[object_id], ic.column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
    WHERE ic.column_id IS NOT NULL
       OR i.filter_definition LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'
  
  RAISERROR('Drop Statistics Referencing Modified Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping statistic ' + id.[Schema] + '.' + id.[TableName] + '.[' + [StatName] + ']'', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP STATISTICS ' + id.[Schema] + '.' + id.[TableName] + '.[' + [StatName] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #StatisticsToDropForChanges id WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Identify Foreign Keys To Drop Based On Column Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #FKsToDropForChanges
  SELECT DISTINCT cc.[Schema], cc.[TableName], FKName = fk.[name]
    INTO #FKsToDropForChanges
    FROM sys.foreign_key_columns fc WITH (NOLOCK)
    LEFT JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.object_id = fc.constraint_object_id
    JOIN #ColumnChanges cc WITH (NOLOCK) ON (OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) = fk.parent_object_id
                                         AND SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) = COL_NAME(fc.[parent_object_id], fc.parent_column_id))
                                         OR (OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) = fk.referenced_object_id
                                         AND SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) = COL_NAME(fc.[referenced_object_id], fc.referenced_column_id))
  
  RAISERROR('Drop Foreign Keys Referencing Modified Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping foreign Key ' + df.[Schema] + '.' + df.[TableName] + '.' + df.[FKName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + df.[Schema] + '.' + df.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + df.[FKName] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #FKsToDropForChanges df WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Defaults To Drop Based On Column Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #DefaultsToDropForChanges
  SELECT cc.[Schema], cc.[TableName], DefaultName = dc.[name]
    INTO #DefaultsToDropForChanges
    FROM sys.default_constraints dc WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON dc.[parent_object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
                                        AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName)
  
  RAISERROR('Drop Defaults Referencing Modified Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping default ' + dd.[Schema] + '.' + dd.[TableName] + '.' + dd.[DefaultName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + dd.[Schema] + '.' + dd.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + dd.[DefaultName] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #DefaultsToDropForChanges dd WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Check Constraints To Drop Based On Column Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ChecksToDropForChanges
  SELECT cc.[Schema], cc.[TableName], CheckName = ck.[name]
    INTO #ChecksToDropForChanges
    FROM sys.check_constraints ck WITH (NOLOCK)
    JOIN #ColumnChanges cc WITH (NOLOCK) ON ck.[parent_object_id] = OBJECT_ID(cc.[Schema] + '.' + cc.[TableName]) 
                                        AND ((ck.parent_column_id <> 0 AND COL_NAME(ck.parent_object_id, ck.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(cc.ColumnName))
                                          OR (ck.parent_column_id = 0 AND ck.[definition] LIKE '%' + SchemaSmith.fn_StripBracketWrapping(cc.ColumnName) + '%'))
  
  RAISERROR('Drop Check Constraints Referencing Modified Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping check constraint ' + fc.[Schema] + '.' + fc.[TableName] + '.' + fc.CheckName + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + fc.[Schema] + '.' + fc.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + fc.CheckName + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ChecksToDropForChanges fc WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Temporarily Disable CDC For Tables With Column Changes', 10, 100) WITH NOWAIT
  IF EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID() AND is_cdc_enabled = 1)
  BEGIN
    SET @v_SQL = ''
    SELECT @v_SQL = @v_SQL +
      'RAISERROR(''  Temporarily disabling CDC on ' + t.[Schema] + '.' + t.[Name] + ' for column modifications'', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
      'EXEC sys.sp_cdc_disable_table @source_schema = N''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', @source_name = N''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''', @capture_instance = N''' + ct.capture_instance + ''';' + CHAR(13) + CHAR(10)
      FROM #Tables t WITH (NOLOCK)
      JOIN sys.tables st WITH (NOLOCK) ON st.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
      JOIN cdc.change_tables ct WITH (NOLOCK) ON ct.source_object_id = st.[object_id]
      WHERE st.is_tracked_by_cdc = 1
        AND (EXISTS (SELECT 1 FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = t.[Schema] AND cc.[TableName] = t.[Name])
          OR EXISTS (SELECT 1 FROM #Columns c WITH (NOLOCK) WHERE c.[Schema] = t.[Schema] AND c.[TableName] = t.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL(c.[ComputedExpression], '')) = ''))
    IF @v_SQL <> ''
    BEGIN
      IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
    END
  END

  RAISERROR('Swap Columns Requiring Data-Preserving Replacement', 10, 100) WITH NOWAIT
  DECLARE @v_SwapSchema NVARCHAR(256), @v_SwapTable NVARCHAR(256), @v_SwapColumn NVARCHAR(256), @v_SwapColumnScript NVARCHAR(MAX)
  DECLARE swap_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT cc.[Schema], cc.[TableName], SchemaSmith.fn_StripBracketWrapping(cc.ColumnName), cc.ColumnScript
      FROM #ColumnChanges cc WITH (NOLOCK)
      WHERE cc.MustSwapColumn = 1
  OPEN swap_cursor
  FETCH NEXT FROM swap_cursor INTO @v_SwapSchema, @v_SwapTable, @v_SwapColumn, @v_SwapColumnScript
  WHILE @@FETCH_STATUS = 0
  BEGIN
    DECLARE @v_TempColName NVARCHAR(256) = @v_SwapColumn + '_swap_temp'
    -- Extract data type without NULL/NOT NULL (used only by non-encrypted path, but declared here to avoid re-declare in cursor loop)
    DECLARE @v_SwapDataType NVARCHAR(MAX) = RTRIM(REPLACE(REPLACE(@v_SwapColumnScript, ' NOT NULL', ''), ' NULL', ''))
    -- Always Encrypted changes on existing columns cannot be performed server-side on a standard
    -- (non-enclave) deployment: the server holds no Column Master Key and cannot re-encrypt data.
    -- Fail closed immediately — before any DDL — in both live and WhatIf mode so the impossibility
    -- is visible in previews. The operator must use the Before/After full-table rebuild instead.
    IF @v_SwapColumnScript LIKE '%ENCRYPTED WITH%'
    BEGIN
      RAISERROR('Always Encrypted: in-place encryption change on %s.%s.[%s] cannot be performed on a standard (non-enclave) server. Use Before/After migration scripts to rebuild the table with the target encryption settings and copy data client-side over a Column Encryption Setting=Enabled connection.', 16, 1, @v_SwapSchema, @v_SwapTable, @v_SwapColumn)
    END
    ELSE
    BEGIN
      -- Non-encrypted swap: add as NULL, copy data, then enforce NOT NULL if needed
      -- Each step must execute separately so SQL Server can resolve column names after ADD
      -- Step 1: Add temp column as NULL (can't add NOT NULL to non-empty table without default)
      SET @v_SQL = 'RAISERROR(''  Swapping column ' + @v_SwapSchema + '.' + @v_SwapTable + '.[' + @v_SwapColumn + '] (data-preserving replacement)'', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                   'ALTER TABLE ' + @v_SwapSchema + '.' + @v_SwapTable + ' ADD [' + @v_TempColName + '] ' + @v_SwapDataType + ' NULL;'
      IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
      -- Step 2: Copy data from original to temp
      SET @v_SQL = 'UPDATE ' + @v_SwapSchema + '.' + @v_SwapTable + ' SET [' + @v_TempColName + '] = [' + @v_SwapColumn + '];'
      IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
      -- Step 3: If original was NOT NULL, enforce it on the temp column now that data is copied
      IF @v_SwapColumnScript LIKE '%NOT NULL%'
      BEGIN
        SET @v_SQL = 'ALTER TABLE ' + @v_SwapSchema + '.' + @v_SwapTable + ' ALTER COLUMN [' + @v_TempColName + '] ' + @v_SwapColumnScript + ';'
        IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
      END
    END
    -- Step 4: Drop original column (shared for both paths)
    SET @v_SQL = 'ALTER TABLE ' + @v_SwapSchema + '.' + @v_SwapTable + ' DROP COLUMN [' + @v_SwapColumn + '];'
    IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
    -- Step 5: Rename temp to original
    SET @v_SQL = 'EXEC sp_rename ''' + SchemaSmith.fn_StripBracketWrapping(@v_SwapSchema) + '.' + SchemaSmith.fn_StripBracketWrapping(@v_SwapTable) + '.[' + @v_TempColName + ']'', ''' + @v_SwapColumn + ''', ''COLUMN'';'
    IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
    FETCH NEXT FROM swap_cursor INTO @v_SwapSchema, @v_SwapTable, @v_SwapColumn, @v_SwapColumnScript
  END
  CLOSE swap_cursor
  DEALLOCATE swap_cursor

  RAISERROR('Drop Modified Computed Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping columns from ' + T.[Schema] + '.' + T.[Name] + ' (' + MessageColumns + ')'', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' DROP ' + ScriptColumns + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name], 
                 ScriptColumns = (SELECT STRING_AGG('COLUMN ' + [ColumnName], ', ') WITHIN GROUP (ORDER BY cc.[ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.MustDropAndRecreate = 1 AND cc.MustSwapColumn = 0),
                 MessageColumns = (SELECT STRING_AGG([ColumnName], ', ') WITHIN GROUP (ORDER BY cc.[ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.MustDropAndRecreate = 1 AND cc.MustSwapColumn = 0)
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 0
              AND EXISTS (SELECT * FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.MustDropAndRecreate = 1 AND cc.MustSwapColumn = 0)) T
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Columns No Longer Part of The Product Definition', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping columns from ' + T.[Schema] + '.' + T.[Name] + ' (' + MessageColumns + ')'', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' DROP ' + ScriptColumns + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM (SELECT T.[Schema], T.[Name],
                 ScriptColumns = (SELECT STRING_AGG('COLUMN ' + [ColumnName], ', ') WITHIN GROUP (ORDER BY [ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.DropOnly = 1),
                 MessageColumns = (SELECT STRING_AGG([ColumnName], ', ') WITHIN GROUP (ORDER BY [ColumnName]) FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.DropOnly = 1)
            FROM #Tables T WITH (NOLOCK)
            WHERE NewTable = 0
              AND @DropColumnsRemovedFromProduct = 1
              AND ISNULL(T.[DropColumnsRemovedFromProduct], 1) = 1
              AND EXISTS (SELECT * FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = T.[Schema] AND cc.[TableName] = T.[Name] AND cc.DropOnly = 1)) T
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Detect Default Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #DefaultChanges
  SELECT C.[Schema], C.[TableName], C.[ColumnName],
         [DefaultName] = (SELECT [Name]
                          FROM sys.default_constraints dc WITH (NOLOCK)
                          WHERE dc.parent_object_id = OBJECT_ID(c.[Schema] + '.' + c.[TableName])
                            AND COL_NAME(dc.parent_object_id, dc.parent_column_id) = SchemaSmith.fn_StripBracketWrapping(C.[ColumnName]))
  INTO #DefaultChanges
  FROM #Tables T WITH (NOLOCK)
           JOIN #Columns c WITH (NOLOCK) ON C.[Schema] = T.[Schema]
      AND C.[TableName] = T.[Name]
      AND C.[NewColumn] = 0
           JOIN INFORMATION_SCHEMA.COLUMNS ic ON ic.TABLE_SCHEMA = SchemaSmith.fn_StripBracketWrapping(C.[Schema])
      AND ic.TABLE_NAME = SchemaSmith.fn_StripBracketWrapping(C.[TableName])
      AND ic.COLUMN_NAME = SchemaSmith.fn_StripBracketWrapping(C.[ColumnName])
  WHERE t.NewTable = 0
    AND SchemaSmith.fn_StripParenWrapping(ic.COLUMN_DEFAULT) <> ISNULL(c.[Default], 'NULL')

  -- Truly new physical columns were added previously, now we need to determine which columns need to be added back due change from computed to physical columns
  UPDATE #Columns 
    SET NewColumn = 0 
    WHERE NewColumn = 1 
      AND RTRIM(ISNULL([ComputedExpression], '')) = ''
  UPDATE c
    SET NewColumn = 1
    FROM #Columns c
    WHERE EXISTS (SELECT * FROM #ColumnChanges cc WITH (NOLOCK) WHERE cc.[Schema] = c.[Schema] AND cc.[TableName] = c.[TableName] and cc.ColumnName = c.ColumnName AND cc.MustDropAndRecreate = 1 AND cc.MustSwapColumn = 0)
  
  RAISERROR('Add missing ProductName extended property to tables', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('EXEC sp_addextendedproperty @name = N''ProductName'', @value = ''' + @ProductName + ''', ' +
                                                              '@level0type = N''Schema'', @level0name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', ' +
                                                              '@level1type = N''Table'', @level1name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Tables t WITH (NOLOCK)
    WHERE NOT EXISTS (SELECT * FROM #TableProperties tp WITH (NOLOCK) WHERE t.[Schema] = tp.[Schema] AND SchemaSmith.fn_StripBracketWrapping(t.[Name]) = tp.TableName AND tp.PropertyName = 'ProductName')
      AND OBJECT_ID(t.[Schema] + '.' + t.[Name]) IS NOT NULL  -- and the table physically exists
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  -- Stamp/refresh the sticky PreventDrop protection marker so it tracks the package value each run
  -- (an existing table newly marked PreventDrop:true gets its property; one toggled back to false is
  -- updated). Covers the same present-table set as the ProductName stamp above (physical tables in #Tables).
  RAISERROR('Stamp/refresh PreventDrop protection marker', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST(
    'IF EXISTS (SELECT 1 FROM fn_listextendedproperty(N''PreventDrop'', N''Schema'', ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', N''Table'', ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''', default, default)) ' +
    'EXEC sp_updateextendedproperty @name = N''PreventDrop'', @value = ''' + CASE WHEN t.[PreventDrop] = 1 THEN 'true' ELSE 'false' END + ''', @level0type = N''Schema'', @level0name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', @level1type = N''Table'', @level1name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + '''; ' +
    'ELSE EXEC sp_addextendedproperty @name = N''PreventDrop'', @value = ''' + CASE WHEN t.[PreventDrop] = 1 THEN 'true' ELSE 'false' END + ''', @level0type = N''Schema'', @level0name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', @level1type = N''Table'', @level1name = ''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #Tables t WITH (NOLOCK)
    WHERE OBJECT_ID(t.[Schema] + '.' + t.[Name]) IS NOT NULL  -- table physically exists
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Add Missing Physical Columns', 10, 100) WITH NOWAIT
  -- Need to do this a second time for the edge case of replacing a computed column with a physical column
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Adding ' + CAST(ColumnCount AS NVARCHAR(100)) + ' new columns to ' + T.[Schema] + '.' + T.[Name] +
                                  CASE WHEN RTRIM(ISNULL(VariantList, '')) <> '' THEN ' (variant: ' + VariantList + ')' ELSE '' END + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + T.[Schema] + '.' + T.[Name] + ' ADD ' + ColumnScripts + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
  FROM (SELECT T.[Schema], T.[Name],
               ColumnScripts = (SELECT STRING_AGG(CAST([ColumnScript] AS NVARCHAR(MAX)), ', ') WITHIN GROUP (ORDER BY c.[ColumnName]) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''),
               ColumnCount = (SELECT COUNT(*) FROM #Columns C WITH (NOLOCK) WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''),
               VariantList = (SELECT STRING_AGG(CAST(REPLACE(RTRIM(c.[VariantName]), '''', '''''') AS NVARCHAR(MAX)), ', ')
                                FROM #Columns C WITH (NOLOCK)
                                WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = ''
                                  AND RTRIM(ISNULL(c.[VariantName], '')) <> '')
        FROM #Tables T WITH (NOLOCK)
        WHERE NewTable = 0
          AND EXISTS (SELECT * FROM #Columns c WHERE C.[Schema] = T.[Schema] AND C.[TableName] = T.[Name] AND c.NewColumn = 1 AND RTRIM(ISNULL([ComputedExpression], '')) = '')) T
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Drop Modified Defaults', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping default ' + dc.[Schema] + '.' + dc.[TableName] + '.' + dc.[DefaultName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + dc.[Schema] + '.' + dc.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + dc.[DefaultName] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #DefaultChanges dc WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing Foreign Keys', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingFKs
  SELECT t.[Schema], [TableName] = t.[Name],
         FKName = fk.[Name],
         FKScript = '(' + (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[parent_object_id], fc.parent_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                             FROM sys.foreign_key_columns fc WITH (NOLOCK)
                             WHERE fk.[object_id] = fc.[constraint_object_id]) + ')' +
                    ' REFERENCES [' + OBJECT_SCHEMA_NAME(referenced_object_id) + '].[' + OBJECT_NAME(referenced_object_id) + '] ' +
                    '(' + (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[referenced_object_id], fc.referenced_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                             FROM sys.foreign_key_columns fc WITH (NOLOCK)
                             WHERE fk.[object_id] = fc.[constraint_object_id]) + ')' +
                    ' ON DELETE ' + REPLACE(fk.delete_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT +
                    ' ON UPDATE ' + REPLACE(fk.update_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT
    INTO #ExistingFKs
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.foreign_keys fk WITH (NOLOCK) ON fk.parent_object_id = OBJECT_ID(t.[Schema] + '.' + t.[Name]) 
    WHERE t.NewTable = 0

  RAISERROR('Detect Foreign Key Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #FKChanges
  SELECT ek.[Schema], ek.[TableName], ek.[FKName]
    INTO #FKChanges
    FROM #ExistingFKs ek WITH (NOLOCK)
    JOIN #ForeignKeys fk WITH (NOLOCK) ON ek.[TableName] = fk.[TableName]
                                      AND ek.[Schema] = fk.[Schema]
                                      AND ek.[FKName] = SchemaSmith.fn_StripBracketWrapping(fk.[KeyName])
    WHERE ek.FKScript <> '(' + [Columns] + ') REFERENCES ' + [RelatedTableSchema] + '.' + [RelatedTable] + ' (' + [RelatedColumns] + ')' +
                         ' ON DELETE ' + [DeleteAction] +
                         ' ON UPDATE ' + [UpdateAction]
  
  RAISERROR('Drop Modified Foreign Keys', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping Foreign Key ' + fc.[Schema] + '.' + fc.[TableName] + '.' + fc.[FKName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + fc.[Schema] + '.' + fc.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + fc.[FKName] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #FKChanges fc WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Collect Existing Statistics Definitions', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingStats
  SELECT t.[Schema], [TableName] = t.[Name], [StatsName] = si.[Name],
         StatisticScript = 'CREATE STATISTICS ' +
                           '[' + si.[Name] + '] ON ' + t.[Schema] + '.' + t.[Name] + ' (' +
                           (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY ic.stats_column_id)
                              FROM sys.stats_columns ic WITH (NOLOCK)
                              WHERE si.[object_id] = ic.[object_id] AND si.stats_id = ic.stats_id) + ')' +
                           CASE WHEN si.has_filter = 1 THEN ' WHERE ' + SchemaSmith.fn_StripParenWrapping(si.filter_definition) ELSE '' END 
    INTO #ExistingStats 
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.stats si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
                                   AND auto_created = 0
                                   AND user_created = 1
                                   AND is_temporary = 0
                                   AND si.[Name] NOT LIKE 'stat[_]%'
                                   AND si.[Name] NOT LIKE 'hind[_]%'
    WHERE t.NewTable = 0
  
  RAISERROR('Detect Statistics Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #StatsChanges
  SELECT s.[Schema], s.[TableName], s.[StatisticName]
    INTO #StatsChanges
    FROM #Statistics s WITH (NOLOCK)
    JOIN #ExistingStats es WITH (NOLOCK) ON s.[Schema] = es.[Schema]
                                        AND s.[TableName] = es.[TableName]
                                        AND SchemaSmith.fn_StripBracketWrapping(s.[StatisticName]) = es.[StatsName]
    WHERE es.StatisticScript <> 'CREATE STATISTICS ' + s.[StatisticName] + ' ON ' + s.[Schema] + '.' + s.[TableName] + ' (' + s.[Columns] + ')' +
                                CASE WHEN RTRIM(ISNULL(s.[FilterExpression], '')) <> '' THEN ' WHERE ' + s.[FilterExpression] ELSE '' END
  
  RAISERROR('Drop Modified Statistics', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping statistics ' + sc.[Schema] + '.' + sc.[TableName] + '.' + sc.[StatisticName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP STATISTICS ' + sc.[Schema] + '.' + sc.[TableName] + '.' + sc.[StatisticName] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #StatsChanges sc WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  -- Drop user-created statistics removed from the product (by-absence), gated by the cascade flag
  -- and per-table tightening. Excludes stats already dropped by the modified pass (#StatsChanges,
  -- still in the product by name) and the column-change pass (#StatisticsToDropForChanges) to avoid
  -- a double DROP STATISTICS. Auto-created stats are already excluded from #ExistingStats.
  RAISERROR('Drop Statistics No Longer Part of The Product Definition', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping statistics ' + es.[Schema] + '.' + es.[TableName] + '.[' + es.[StatsName] + ']'', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP STATISTICS ' + es.[Schema] + '.' + es.[TableName] + '.[' + es.[StatsName] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ExistingStats es WITH (NOLOCK)
    JOIN #Tables t WITH (NOLOCK) ON t.[Schema] = es.[Schema] AND t.[Name] = es.[TableName]
    WHERE t.NewTable = 0
      AND @DropStatisticsRemovedFromProduct = 1
      AND ISNULL(t.[DropStatisticsRemovedFromProduct], 1) = 1
      AND NOT EXISTS (SELECT * FROM #Statistics s WITH (NOLOCK)
                        WHERE es.[Schema] = s.[Schema]
                          AND es.[TableName] = s.[TableName]
                          AND es.[StatsName] = SchemaSmith.fn_StripBracketWrapping(s.[StatisticName]))
      AND NOT EXISTS (SELECT * FROM #StatisticsToDropForChanges sd WITH (NOLOCK)
                        WHERE es.[Schema] = sd.[Schema]
                          AND es.[TableName] = sd.[TableName]
                          AND es.[StatsName] = sd.[StatName])
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Collect Existing Check Constraints', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #ExistingCheckConstraints
  SELECT t.[Schema], [TableName] = t.[Name], [CheckName] = ck.[name], 
         [CheckColumn] = CASE WHEN ck.parent_column_id <> 0 THEN COL_NAME(ck.parent_object_id, ck.parent_column_id) ELSE NULL END,
         [CheckDefinition] = SchemaSmith.fn_StripParenWrapping(ck.[definition])
    INTO #ExistingCheckConstraints
    FROM #Tables t WITH (NOLOCK)
    JOIN sys.check_constraints ck WITH (NOLOCK) ON ck.[parent_object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
  
  RAISERROR('Detect Column Level Check Constraint Changes', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #CheckChanges
  SELECT ec.[Schema], ec.[TableName], ec.[CheckName]
    INTO #CheckChanges
    FROM #ExistingCheckConstraints ec WITH (NOLOCK)
    JOIN #Columns c WITH (NOLOCK) ON ec.[Schema] = c.[Schema]
                                 AND ec.[TableName] = c.[TableName]
                                 AND ec.[CheckColumn] = SchemaSmith.fn_StripBracketWrapping(c.[ColumnName])
    WHERE ec.[CheckColumn] IS NOT NULL
      AND ec.[CheckDefinition] <> ISNULL(c.[CheckExpression], '')
      AND NOT EXISTS (SELECT * 
                        FROM #CheckConstraints cc WITH (NOLOCK) 
                        WHERE ec.[Schema] = cc.[Schema]
                          AND ec.[TableName] = cc.[TableName]
                          AND ec.[CheckName] = SchemaSmith.fn_StripBracketWrapping(cc.[ConstraintName]))

  RAISERROR('Detect Table Level Check Constraint Changes', 10, 100) WITH NOWAIT
  INSERT #CheckChanges ([Schema], [TableName], [CheckName])
    SELECT ec.[Schema], ec.[TableName], ec.[CheckName]
      FROM #ExistingCheckConstraints ec WITH (NOLOCK)
      JOIN #CheckConstraints cc WITH (NOLOCK) ON ec.[Schema] = cc.[Schema]
                                             AND ec.[TableName] = cc.[TableName]
                                             AND ec.[CheckName] = SchemaSmith.fn_StripBracketWrapping(cc.[ConstraintName])
      WHERE ec.[CheckDefinition] <> cc.[Expression]
  
  RAISERROR('Drop Modified Check Constraints', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping check constraint ' + cc.[Schema] + '.' + cc.[TableName] + '.' + cc.[CheckName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + cc.[Schema] + '.' + cc.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + cc.[CheckName] + '];' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #CheckChanges cc WITH (NOLOCK)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  -- Drop table-level check constraints removed from the product (by-absence). Column-level checks
  -- (CheckColumn IS NOT NULL) are owned by the column modify pass above; only table-level checks
  -- absent from the product's CheckConstraints are dropped here, gated by the cascade flag + the
  -- per-table tightening (a table may set DropCheckConstraintsRemovedFromProduct:false to protect its own).
  RAISERROR('Drop Check Constraints No Longer Part of The Product Definition', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping check constraint ' + ec.[Schema] + '.' + ec.[TableName] + '.' + ec.[CheckName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + ec.[Schema] + '.' + ec.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + ec.[CheckName] + '];' + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''constraint'', ''' + ec.[Schema] + '.' + ec.[TableName] + '.' + ec.[CheckName] + ''', ''dropped'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ExistingCheckConstraints ec WITH (NOLOCK)
    JOIN #Tables t WITH (NOLOCK) ON t.[Schema] = ec.[Schema] AND t.[Name] = ec.[TableName]
    WHERE ec.[CheckColumn] IS NULL
      AND t.NewTable = 0
      AND @DropCheckConstraintsRemovedFromProduct = 1
      AND ISNULL(t.[DropCheckConstraintsRemovedFromProduct], 1) = 1
      AND NOT EXISTS (SELECT * FROM #CheckConstraints cc WITH (NOLOCK)
                        WHERE ec.[Schema] = cc.[Schema]
                          AND ec.[TableName] = cc.[TableName]
                          AND ec.[CheckName] = SchemaSmith.fn_StripBracketWrapping(cc.[ConstraintName]))
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)

  RAISERROR('Alter Modified Columns', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Altering Column ' + cc.[Schema] + '.' + cc.[TableName] + '.' + cc.[ColumnName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'ALTER TABLE ' + cc.[Schema] + '.' + cc.[TableName] + ' ALTER COLUMN ' + cc.[ColumnName] + ' ' +
                                  CASE WHEN RTRIM([SpecialColumnScript]) <> '' THEN [SpecialColumnScript] ELSE [ColumnScript] END + ';' + CHAR(13) + CHAR(10) +
                                  'INSERT INTO SchemaSmith.ChangeAudit (SessionId, ObjectType, ObjectName, ActionType) VALUES (@@SPID, ''column'', ''' + cc.[Schema] + '.' + cc.[TableName] + '.' + cc.[ColumnName] + ''', ''modified'');' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
        FROM #ColumnChanges cc WITH (NOLOCK)
        WHERE [MustDropAndRecreate] = 0
          AND [MustSwapColumn] = 0
          AND [DropOnly] = 0
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Identify Existing Clustered Index Conflicts', 10, 100) WITH NOWAIT
  DROP TABLE IF EXISTS #MissingClusteredIndexTables
  SELECT DISTINCT i.[Schema], i.[TableName]
    INTO #MissingClusteredIndexTables
    FROM #Indexes i WITH (NOLOCK)
    WHERE i.[Clustered] = 1
      AND NOT EXISTS (SELECT * 
                        FROM sys.indexes si WITH (NOLOCK)
                        WHERE si.[object_id] = OBJECT_ID(i.[Schema] + '.' + i.[TableName]) 
                          AND si.[name] = SchemaSmith.fn_StripBracketWrapping(i.[IndexName]))
  
  RAISERROR('Drop Conflicting Clustered Index', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping ' + CASE WHEN si.is_primary_key = 1 OR si.is_unique_constraint = 1 THEN 'constraint' ELSE 'index' END + ' ' + mct.[Schema] + '.' + mct.[TableName] + '.' + si.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  CASE WHEN si.is_primary_key = 1 OR si.is_unique_constraint = 1
                                       THEN 'ALTER TABLE ' + mct.[Schema] + '.' + mct.[TableName] + ' DROP CONSTRAINT IF EXISTS [' + si.[Name] + '];'
                                       ELSE 'DROP INDEX IF EXISTS [' + si.[Name] + '] ON ' + mct.[Schema] + '.' + mct.[TableName] + ';'
                                       END AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #MissingClusteredIndexTables mct WITH (NOLOCK)
    JOIN sys.indexes si WITH (NOLOCK) ON si.[object_id] = OBJECT_ID(mct.[Schema] + '.' + mct.[TableName])
                                     AND si.[type] IN (1, 5)
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Drop Modified or Removed FullText Indexes', 10, 100) WITH NOWAIT
  SELECT @v_SQL = STRING_AGG(CAST('RAISERROR(''  Dropping fulltext index on ' + ei.[Schema] + '.' + ei.[TableName] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                                  'DROP FULLTEXT INDEX ON ' + ei.[Schema] + '.' + ei.[TableName] + ';' AS NVARCHAR(MAX)), CHAR(13) + CHAR(10))
    FROM #ExistingFullTextIndexes ei WITH (NOLOCK)
    LEFT JOIN #FullTextIndexes fi WITH (NOLOCK) ON fi.[Schema] = ei.[Schema]
                                               AND fi.[TableName] = ei.[TableName]
    JOIN sys.fulltext_indexes ft WITH (NOLOCK) ON ft.[object_id] = OBJECT_ID(ei.[Schema] + '.' + ei.[TableName])
    WHERE RTRIM(ISNULL(fi.[Columns], '')) <> RTRIM(ISNULL(ei.[Columns], ''))
       OR SchemaSmith.fn_StripBracketWrapping(fi.[FullTextCatalog]) <> SchemaSmith.fn_StripBracketWrapping(ei.[FullTextCatalog])
       OR SchemaSmith.fn_StripBracketWrapping(fi.[KeyIndex]) <> SchemaSmith.fn_StripBracketWrapping(ei.[KeyIndex])
       OR fi.[ChangeTracking] <> ei.[ChangeTracking]
       OR RTRIM(ISNULL(fi.[StopList], '')) <> RTRIM(ISNULL(ei.[StopList], ''))
       OR fi.[TableName] IS NULL
  IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
  
  RAISERROR('Enable/Disable CDC', 10, 100) WITH NOWAIT
  IF EXISTS (SELECT 1 FROM sys.databases WHERE database_id = DB_ID() AND is_cdc_enabled = 1)
  BEGIN
    SET @v_SQL = ''
    SELECT @v_SQL = @v_SQL +
      CASE WHEN t.EnableCDC = 1 AND st.is_tracked_by_cdc = 0
           THEN 'RAISERROR(''  Enable CDC on ' + t.[Schema] + '.' + t.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                'EXEC sys.sp_cdc_enable_table @source_schema = N''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', @source_name = N''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''', @role_name = NULL;' + CHAR(13) + CHAR(10)
           WHEN t.EnableCDC = 0 AND st.is_tracked_by_cdc = 1
           THEN 'RAISERROR(''  Disable CDC on ' + t.[Schema] + '.' + t.[Name] + ''', 10, 100) WITH NOWAIT;' + CHAR(13) + CHAR(10) +
                'EXEC sys.sp_cdc_disable_table @source_schema = N''' + SchemaSmith.fn_StripBracketWrapping(t.[Schema]) + ''', @source_name = N''' + SchemaSmith.fn_StripBracketWrapping(t.[Name]) + ''', @capture_instance = N''' + ct.capture_instance + ''';' + CHAR(13) + CHAR(10)
           ELSE '' END
      FROM #Tables t WITH (NOLOCK)
      JOIN sys.tables st WITH (NOLOCK) ON st.[object_id] = OBJECT_ID(t.[Schema] + '.' + t.[Name])
      LEFT JOIN cdc.change_tables ct WITH (NOLOCK) ON ct.source_object_id = st.[object_id]
      WHERE (t.EnableCDC = 1 AND st.is_tracked_by_cdc = 0)
         OR (t.EnableCDC = 0 AND st.is_tracked_by_cdc = 1)
    IF @v_SQL <> ''
    BEGIN
      IF @WhatIf = 1 EXEC SchemaSmith.PrintWithNoWait @v_SQL ELSE EXEC(@v_SQL)
    END
  END

  SET NOCOUNT OFF
END TRY
BEGIN CATCH
  THROW
END CATCH