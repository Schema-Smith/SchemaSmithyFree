CREATE OR ALTER PROCEDURE SchemaSmith.GenerateTableJSON 
  @p_Schema SYSNAME = 'dbo',
  @p_Table SYSNAME
AS
SET NOCOUNT ON
DECLARE @v_DatabaseCollation NVARCHAR(200) = CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS NVARCHAR(200))
SELECT [Line] FROM SchemaSmith.fn_FormatJson(REPLACE(REPLACE(REPLACE((
SELECT '[' + TABLE_SCHEMA + ']' AS [Schema],
       '[' + TABLE_NAME + ']' AS [Name],
       COALESCE((SELECT p.data_compression_desc COLLATE DATABASE_DEFAULT
                   FROM sys.partitions AS p WITH (NOLOCK) 
                   WHERE p.[object_id] = st.[object_id]
                     AND p.index_id < 2), 'NONE') AS [CompressionType],
       '' AS [OldName],
	   (SELECT * 
          FROM (SELECT '[' + c.COLUMN_NAME + ']' AS [Name],
                       UPPER(USER_TYPE) + CASE WHEN USER_TYPE LIKE '%CHAR' OR USER_TYPE LIKE '%BINARY'
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
                                          CASE WHEN ic.column_id IS NOT NULL
                                               THEN ' IDENTITY(' + CONVERT(NVARCHAR(20), ic.seed_value) + ', ' + CONVERT(NVARCHAR(20), ic.increment_value) + ')' +
                                                    CASE WHEN ic.is_not_for_replication = 1 THEN ' NOT FOR REPLICATION' ELSE '' END
                                               ELSE '' END AS [DataType],                   
                       CAST(CASE WHEN c.IS_NULLABLE = 'Yes' THEN 1 ELSE 0 END AS BIT) AS [Nullable],
		               NULLIF(SchemaSmith.fn_StripParenWrapping(COLUMN_DEFAULT), 'NULL') AS [Default],
                       (SELECT SchemaSmith.fn_StripParenWrapping([definition])
                          FROM sys.check_constraints WITH (NOLOCK)
                          WHERE parent_object_id = st.[object_id]
                            AND parent_column_id = COLUMNPROPERTY(st.[object_id], c.COLUMN_NAME, 'ColumnId')) AS [CheckExpression],
                       SchemaSmith.fn_StripParenWrapping(cc.[definition]) AS ComputedExpression,
                       ISNULL(cc.is_persisted, CAST(0 AS BIT)) AS [Persisted],
                       sc.is_sparse AS [Sparse],
                       ISNULL(NULLIF(ic.COLLATION_NAME, @v_DatabaseCollation), '') AS [Collation],
                       ISNULL(mc.masking_function, '') COLLATE DATABASE_DEFAULT AS DataMaskFunction,
                       '' AS [OldName],
	                   '{' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Column', c.COLUMN_NAME) x) + '}' AS [ExtendedProperties]
                  FROM INFORMATION_SCHEMA.COLUMNS c WITH (NOLOCK)
                  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = st.[object_id] AND sc.[name] = c.COLUMN_NAME
                  JOIN (SELECT CASE WHEN SCHEMA_NAME(typ.[schema_id]) IN ('sys', 'dbo')
                                    THEN '' ELSE SCHEMA_NAME(typ.[schema_id]) + '.' END + typ.[name] AS USER_TYPE, typ.user_type_id
                          FROM sys.types typ WITH (NOLOCK)) ut ON ut.user_type_id = sc.user_type_id
                  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[name] = c.COLUMN_NAME
                                                                 AND cc.[object_id] = st.[object_id]
                  LEFT JOIN sys.identity_columns ic WITH (NOLOCK) ON ic.[Name] = c.COLUMN_NAME
                                                                 AND ic.[object_id] = st.[object_id]
                  LEFT JOIN sys.masked_columns mc WITH (NOLOCK) ON mc.[object_id] = st.[object_id]
                                                               AND mc.[name] = c.COLUMN_NAME
                  WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA
                    AND c.TABLE_NAME = t.TABLE_NAME) x 
          ORDER BY [Name]
          FOR JSON AUTO) AS [Columns],
       (SELECT '[' + [Name] + ']' AS [Name], 
               (SELECT p.data_compression_desc COLLATE DATABASE_DEFAULT
                  FROM sys.partitions AS p WITH (NOLOCK) 
                  WHERE p.[object_id] = st.[object_id]
                    AND p.index_id = si.index_id) AS [CompressionType],
               is_primary_key AS [PrimaryKey], 
               is_unique AS [Unique],
               is_unique_constraint AS [UniqueConstraint], 
               CAST(CASE WHEN [type] IN (1, 5) THEN 1 ELSE 0 END AS BIT) AS [Clustered], 
               CAST(CASE WHEN [type] IN (5, 6) THEN 1 ELSE 0 END AS BIT) AS [ColumnStore], 
               CASE WHEN fill_factor = 100 THEN 0 ELSE fill_factor END AS [FillFactor],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY key_ordinal)
                  FROM sys.index_columns ic WITH (NOLOCK)
                  WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 0) AS [IndexColumns],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY index_column_id)
                  FROM sys.index_columns ic WITH (NOLOCK)
                  WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) AS [IncludeColumns],
			   CASE WHEN has_filter = 1 THEN SchemaSmith.fn_StripParenWrapping(filter_definition) ELSE NULL END AS [FilterExpression],
			   '{' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + [Value] + '"' AS NVARCHAR(MAX)), ',') 
                        FROM (SELECT ISNULL(i.[Name], c.[Name]) AS [Name], RTRIM(COALESCE(CONVERT(NVARCHAR(MAX), c.[Value]) + ' ', '') + COALESCE(CONVERT(NVARCHAR(MAX), i.[Value]), '')) AS [Value]
                                FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Index', si.[Name]) i
                                FULL OUTER JOIN fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Constraint', si.[Name]) c ON i.[Name] = c.[Name]) x)
                   + '}' AS [ExtendedProperties]
          FROM sys.indexes si WITH (NOLOCK)
          WHERE si.[object_id] = st.[object_id]
            AND NOT EXISTS (SELECT * FROM sys.xml_indexes xi WITH (NOLOCK) WHERE xi.[object_id] = si.[object_id] AND xi.index_id = si.index_id)
            AND is_hypothetical = 0
            AND is_disabled = 0
            AND index_id > 0
          ORDER BY [Name]
          FOR JSON AUTO) AS [Indexes],
       (SELECT '[' + i.[name] COLLATE DATABASE_DEFAULT + ']' AS [Name],
               '[' + COL_NAME(i.[Object_id], ic.column_id) + ']' AS [Column],
               CONVERT(BIT, CASE WHEN i.xml_index_type = 0 THEN 1 ELSE 0 END) AS [IsPrimary],
               (SELECT '[' + [Name] COLLATE DATABASE_DEFAULT + ']' FROM sys.xml_indexes i2 WHERE i2.[object_id] = i.[object_id] AND i2.index_id = i.using_xml_index_id AND i.xml_index_type = 1) AS [PrimaryIndex],
               i.secondary_type_desc COLLATE DATABASE_DEFAULT AS [SecondaryIndexType],
			   '{' + (SELECT STRING_AGG(CAST('"' + x.[Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Index', i.[Name]) x) + '}' AS [ExtendedProperties]
          FROM sys.xml_indexes i WITH (NOLOCK)
          JOIN sys.index_columns ic WITH (NOLOCK) ON i.[object_id] = ic.[object_id] AND i.index_id = ic.index_id
          WHERE i.[object_id] = st.[object_id]
          ORDER BY i.[Name]
          FOR JSON AUTO) AS [XmlIndexes],
	   (SELECT '[' + [Name] + ']' AS [Name],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[parent_object_id], fc.parent_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                            FROM sys.foreign_key_columns fc WITH (NOLOCK)
                            WHERE fk.[object_id] = fc.[constraint_object_id]) AS [Columns],
               '[' + OBJECT_SCHEMA_NAME(referenced_object_id) + ']' AS RelatedTableSchema,
               '[' + OBJECT_NAME(referenced_object_id) + ']' AS RelatedTable,
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[referenced_object_id], fc.referenced_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                            FROM sys.foreign_key_columns fc WITH (NOLOCK)
                            WHERE fk.[object_id] = fc.[constraint_object_id]) AS [RelatedColumns],
               REPLACE(fk.delete_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT AS [DeleteAction],
               REPLACE(fk.update_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT AS [UpdateAction],
               '{' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Constraint', fk.[Name]) x) + '}' AS [ExtendedProperties]
          FROM sys.foreign_keys fk WITH (NOLOCK)
          WHERE fk.parent_object_id = st.[object_id]
          ORDER BY [Name]
          FOR JSON AUTO) AS [ForeignKeys],
       (SELECT '[' + [Name] + ']' AS [Name], 
               (SELECT STRING_AGG(CAST('[' + COL_NAME(sc.[object_id], sc.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY sc.stats_column_id)
                  FROM sys.stats_columns sc WITH (NOLOCK)
                  WHERE s.[object_id] = sc.[object_id] AND s.stats_id = sc.stats_id) AS [Columns],
               SchemaSmith.fn_StripParenWrapping([filter_definition]) AS FilterExpression,
			   '{' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Statistic', s.[Name]) x) + '}' AS [ExtendedProperties]
          FROM sys.stats s WITH (NOLOCK)
          WHERE [object_id] = st.[object_id]
            AND auto_created = 0
            AND user_created = 1
            AND is_temporary = 0
            AND [Name] NOT LIKE 'stat[_]%'
            AND [Name] NOT LIKE 'hind[_]%'
          ORDER BY [Name]
          FOR JSON AUTO) AS [Statistics],
       (SELECT '[' + [Name] + ']' AS [Name],
               SchemaSmith.fn_StripParenWrapping([definition]) AS [Expression],
               '{' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, 'Constraint', cc.[Name]) x) + '}' AS [ExtendedProperties]
          FROM sys.check_constraints cc WITH (NOLOCK)
          WHERE parent_object_id = st.[object_id]
            AND parent_column_id = 0
          ORDER BY [Name]
          FOR JSON AUTO) AS [CheckConstraints],
       (SELECT FullTextCatalog = '[' + (SELECT c.[name] FROM sys.fulltext_catalogs c WITH (NOLOCK) WHERE c.fulltext_catalog_id = fi.fulltext_catalog_id) + ']',
               KeyIndex = '[' + (SELECT i.[Name] FROM sys.indexes i WITH (NOLOCK) WHERE i.[object_id] = fi.[object_id] AND i.[index_id] = fi.[unique_index_id]) + ']',
               ChangeTracking = change_tracking_state_desc,
               [StopList] = '[' + (SELECT fs.[name] FROM sys.fulltext_stoplists fs WITH (NOLOCK) WHERE fs.stoplist_id = fi.stoplist_id) + ']',
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[object_id], fc.column_id) + ']' +
                                       CASE WHEN fc.type_column_id IS NOT NULL
                                            THEN ' TYPE COLUMN [' + COL_NAME(fc.[object_id], fc.type_column_id) + ']'
                                            ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY COL_NAME(fc.[object_id], fc.column_id))
                  FROM sys.fulltext_index_columns fc WITH (NOLOCK)
                  WHERE fi.[object_id] = fc.[object_id]) AS [Columns]
          FROM sys.fulltext_indexes fi WITH (NOLOCK)
          WHERE [object_id] = st.[object_id]
          FOR JSON PATH,WITHOUT_ARRAY_WRAPPER) AS [FullTextIndex],
	   '{' + (SELECT STRING_AGG(CAST('"' + [Name] + '": "' + CONVERT(NVARCHAR(MAX), [Value]) + '"' AS NVARCHAR(MAX)), ',') FROM fn_listextendedproperty(default, 'Schema', @p_Schema, 'Table', @p_Table, default, default) x) + '}' AS [ExtendedProperties]
  FROM INFORMATION_SCHEMA.TABLES t WITH (NOLOCK)
  JOIN sys.tables st WITH (NOLOCK) ON st.[object_id] = OBJECT_ID(@p_Schema + '.' + @p_Table)
  WHERE TABLE_NAME = @p_Table
    AND TABLE_SCHEMA = @p_Schema
  FOR JSON AUTO, WITHOUT_ARRAY_WRAPPER
), '\"', '"'), '"}"', '" }'), '"{"', '{ "'), 1)
ORDER BY [LineNo]