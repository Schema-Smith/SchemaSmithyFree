-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- FOR XML PATH twin of SchemaSmith.GenerateTableJSON.sql for the compare/extraction side below the
-- FOR JSON binary floor (SQL Server pre-2016, which lacks FOR JSON/JSON_QUERY entirely). Emits the SAME
-- object shape as the JSON proc but as XML: each array container (Columns/Indexes/XmlIndexes/ForeignKeys/
-- Statistics/CheckConstraints) is a repeated element carrying json:Array="true" so a single-element array
-- does not collapse to an object when C# (ModelXmlSerializer.FromIngestXml -> SerializeXNode) converts it
-- back to JSON for PlatformDeserializer.DeserializeTable. bit values are emitted as 'true'/'false' text so
-- Newtonsoft coerces them into the typed model. No fn_FormatJson/REPLACE wrapper is needed (FOR XML PATH
-- emits well-formed, entity-encoded XML directly).
--
-- Extensions bag is DROPPED on the legacy encoding (program design non-goal); the equivalence test asserts
-- model equality minus Extensions. The temporal catalog columns (temporal_type/generated_always_type) are
-- 2016+ and are gated in Slice E when the floor drops below 2016 (mirrors GenerateTableJSON).
CREATE OR ALTER PROCEDURE SchemaSmith.GenerateTableXml
  @p_Schema SYSNAME = 'dbo',
  @p_Table SYSNAME
AS
SET NOCOUNT ON
DECLARE @v_DatabaseCollation NVARCHAR(200) = CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS NVARCHAR(200))
;WITH XMLNAMESPACES ('http://james.newtonking.com/projects/json' AS json)
SELECT '[' + TABLE_SCHEMA + ']' AS [Schema],
       '[' + TABLE_NAME + ']' AS [Name],
       COALESCE((SELECT p.data_compression_desc COLLATE DATABASE_DEFAULT
                   FROM sys.partitions AS p WITH (NOLOCK)
                   WHERE p.[object_id] = st.[object_id]
                     AND p.index_id < 2), 'NONE') AS [CompressionType],
       CASE WHEN st.is_tracked_by_cdc = 1 THEN 'true' ELSE 'false' END AS [EnableCDC],
       -- System-versioning round-trip (#369): emit IsTemporal only when true. sys.tables.temporal_type is
       -- 2016+ (safe at the current 2017 floor; gate this + generated_always_type below when < 2016).
       CASE WHEN st.temporal_type = 2 THEN 'true' END AS [IsTemporal],
       -- Sticky drop-protection marker (only when set true). Read from the PreventDrop extended property. #270
       CASE WHEN (SELECT CONVERT(NVARCHAR(50), [value])
                    FROM fn_listextendedproperty(N'PreventDrop', N'Schema', @p_Schema, N'Table', @p_Table, default, default)) = 'true'
            THEN 'true' END AS [PreventDrop],
       '' AS [OldName],
       '' AS [ContentFile],
       'NONE' AS [MergeType],
       (SELECT 'true' AS [@json:Array],
                       '[' + c.COLUMN_NAME + ']' AS [Name],
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
                       CASE WHEN c.IS_NULLABLE = 'Yes' THEN 'true' ELSE 'false' END AS [Nullable],
		               NULLIF(SchemaSmith.fn_StripParenWrapping(COLUMN_DEFAULT), 'NULL') AS [Default],
                       (SELECT SchemaSmith.fn_StripParenWrapping([definition])
                          FROM sys.check_constraints WITH (NOLOCK)
                          WHERE parent_object_id = st.[object_id]
                            AND parent_column_id = sc.column_id) AS [CheckExpression],
                       SchemaSmith.fn_StripParenWrapping(cc.[definition]) AS ComputedExpression,
                       CASE WHEN ISNULL(cc.is_persisted, 0) = 1 THEN 'true' ELSE 'false' END AS [Persisted],
                       CASE WHEN sc.is_sparse = 1 THEN 'true' ELSE 'false' END AS [Sparse],
                       ISNULL(NULLIF(ic.COLLATION_NAME, @v_DatabaseCollation), '') AS [Collation],
                       ISNULL(mc.masking_function, '') COLLATE DATABASE_DEFAULT AS DataMaskFunction,
                       ISNULL(sc.encryption_type_desc, 'NONE') COLLATE DATABASE_DEFAULT AS EncryptionType,
                       ISNULL((SELECT '[' + cek.[name] + ']'
                                 FROM sys.column_encryption_keys cek WITH (NOLOCK)
                                WHERE cek.column_encryption_key_id = sc.column_encryption_key_id), '') COLLATE DATABASE_DEFAULT AS EncryptionKey,
                       ISNULL(sc.encryption_algorithm_name, '') COLLATE DATABASE_DEFAULT AS EncryptionAlgorithm,
                       '' AS [OldName]
                  FROM INFORMATION_SCHEMA.COLUMNS c WITH (NOLOCK)
                  JOIN sys.columns sc WITH (NOLOCK) ON sc.[object_id] = st.[object_id] AND sc.[name] = c.COLUMN_NAME
                  JOIN (SELECT CASE WHEN SCHEMA_NAME(typ.[schema_id]) IN ('sys', 'dbo')
                                    THEN '' ELSE SCHEMA_NAME(typ.[schema_id]) + '.' END + typ.[name] AS USER_TYPE, typ.user_type_id
                          FROM sys.types typ WITH (NOLOCK)) ut ON ut.user_type_id = sc.user_type_id
                  LEFT JOIN sys.computed_columns cc WITH (NOLOCK) ON cc.[object_id] = st.[object_id]
                                                                 AND cc.[name] = c.COLUMN_NAME
                  LEFT JOIN sys.identity_columns ic WITH (NOLOCK) ON ic.[object_id] = st.[object_id]
                                                                 AND ic.[Name] = c.COLUMN_NAME
                  LEFT JOIN sys.masked_columns mc WITH (NOLOCK) ON mc.[object_id] = st.[object_id]
                                                               AND mc.[name] = c.COLUMN_NAME
                  WHERE c.TABLE_SCHEMA = t.TABLE_SCHEMA
                    AND c.TABLE_NAME = t.TABLE_NAME
                    -- Exclude the temporal period columns (GENERATED ALWAYS AS ROW START/END); regenerated
                    -- from IsTemporal on apply (#369).
                    AND sc.generated_always_type = 0
                  ORDER BY c.COLUMN_NAME
                  FOR XML PATH('Columns'), TYPE),
       (SELECT 'true' AS [@json:Array],
               '[' + [Name] + ']' AS [Name],
               (SELECT p.data_compression_desc COLLATE DATABASE_DEFAULT
                  FROM sys.partitions AS p WITH (NOLOCK)
                  WHERE p.[object_id] = si.[object_id]
                    AND p.index_id = si.index_id) AS [CompressionType],
               CASE WHEN is_primary_key = 1 THEN 'true' ELSE 'false' END AS [PrimaryKey],
               CASE WHEN is_unique = 1 THEN 'true' ELSE 'false' END AS [Unique],
               CASE WHEN is_unique_constraint = 1 THEN 'true' ELSE 'false' END AS [UniqueConstraint],
               CASE WHEN [type] IN (1, 5) THEN 'true' ELSE 'false' END AS [Clustered],
               CASE WHEN [type] IN (5, 6) THEN 'true' ELSE 'false' END AS [ColumnStore],
               CASE WHEN fill_factor = 100 THEN 0 ELSE fill_factor END AS [FillFactor],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' + CASE WHEN ic.is_descending_key = 1 THEN ' DESC' ELSE '' END AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY key_ordinal)
                  FROM sys.index_columns ic WITH (NOLOCK)
                  WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 0) AS [IndexColumns],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(ic.[object_id], ic.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY index_column_id)
                  FROM sys.index_columns ic WITH (NOLOCK)
                  WHERE si.[object_id] = ic.[object_id] AND si.index_id = ic.index_id AND is_included_column = 1) AS [IncludeColumns],
			   CASE WHEN has_filter = 1 THEN SchemaSmith.fn_StripParenWrapping(filter_definition) ELSE NULL END AS [FilterExpression]
          FROM sys.indexes si WITH (NOLOCK)
          WHERE si.[object_id] = st.[object_id]
            AND NOT EXISTS (SELECT * FROM sys.xml_indexes xi WITH (NOLOCK) WHERE xi.[object_id] = si.[object_id] AND xi.index_id = si.index_id)
            AND is_hypothetical = 0
            AND is_disabled = 0
            AND index_id > 0
          ORDER BY [Name]
          FOR XML PATH('Indexes'), TYPE),
       (SELECT 'true' AS [@json:Array],
               '[' + i.[name] COLLATE DATABASE_DEFAULT + ']' AS [Name],
               '[' + COL_NAME(i.[Object_id], ic.column_id) + ']' AS [Column],
               CASE WHEN i.xml_index_type = 0 THEN 'true' ELSE 'false' END AS [IsPrimary],
               (SELECT '[' + [Name] COLLATE DATABASE_DEFAULT + ']' FROM sys.xml_indexes i2 WHERE i2.[object_id] = i.[object_id] AND i2.index_id = i.using_xml_index_id AND i.xml_index_type = 1) AS [PrimaryIndex],
               i.secondary_type_desc COLLATE DATABASE_DEFAULT AS [SecondaryIndexType]
          FROM sys.xml_indexes i WITH (NOLOCK)
          JOIN sys.index_columns ic WITH (NOLOCK) ON i.[object_id] = ic.[object_id] AND i.index_id = ic.index_id
          WHERE i.[object_id] = st.[object_id]
          ORDER BY i.[Name]
          FOR XML PATH('XmlIndexes'), TYPE),
	   (SELECT 'true' AS [@json:Array],
               '[' + [Name] + ']' AS [Name],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[parent_object_id], fc.parent_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                            FROM sys.foreign_key_columns fc WITH (NOLOCK)
                            WHERE fk.[object_id] = fc.[constraint_object_id]) AS [Columns],
               '[' + OBJECT_SCHEMA_NAME(referenced_object_id) + ']' AS RelatedTableSchema,
               '[' + OBJECT_NAME(referenced_object_id) + ']' AS RelatedTable,
               (SELECT STRING_AGG(CAST('[' + COL_NAME(fc.[referenced_object_id], fc.referenced_column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY fc.constraint_column_id)
                            FROM sys.foreign_key_columns fc WITH (NOLOCK)
                            WHERE fk.[object_id] = fc.[constraint_object_id]) AS [RelatedColumns],
               REPLACE(fk.delete_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT AS [DeleteAction],
               REPLACE(fk.update_referential_action_desc, '_', ' ') COLLATE DATABASE_DEFAULT AS [UpdateAction]
          FROM sys.foreign_keys fk WITH (NOLOCK)
          WHERE fk.parent_object_id = st.[object_id]
          ORDER BY [Name]
          FOR XML PATH('ForeignKeys'), TYPE),
       (SELECT 'true' AS [@json:Array],
               '[' + [Name] + ']' AS [Name],
               (SELECT STRING_AGG(CAST('[' + COL_NAME(sc.[object_id], sc.column_id) + ']' AS NVARCHAR(MAX)), ',') WITHIN GROUP (ORDER BY sc.stats_column_id)
                  FROM sys.stats_columns sc WITH (NOLOCK)
                  WHERE s.[object_id] = sc.[object_id] AND s.stats_id = sc.stats_id) AS [Columns],
               SchemaSmith.fn_StripParenWrapping([filter_definition]) AS FilterExpression
          FROM sys.stats s WITH (NOLOCK)
          WHERE [object_id] = st.[object_id]
            AND auto_created = 0
            AND user_created = 1
            AND is_temporary = 0
            AND [Name] NOT LIKE 'stat[_]%'
            AND [Name] NOT LIKE 'hind[_]%'
          ORDER BY [Name]
          FOR XML PATH('Statistics'), TYPE),
       (SELECT 'true' AS [@json:Array],
               '[' + [Name] + ']' AS [Name],
               SchemaSmith.fn_StripParenWrapping([definition]) AS [Expression]
          FROM sys.check_constraints cc WITH (NOLOCK)
          WHERE parent_object_id = st.[object_id]
            AND parent_column_id = 0
          ORDER BY [Name]
          FOR XML PATH('CheckConstraints'), TYPE),
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
          WHERE fi.[object_id] = st.[object_id]
          FOR XML PATH('FullTextIndex'), TYPE)
  FROM INFORMATION_SCHEMA.TABLES t WITH (NOLOCK)
  JOIN sys.tables st WITH (NOLOCK) ON st.[object_id] = OBJECT_ID(@p_Schema + '.' + @p_Table)
  WHERE TABLE_NAME = @p_Table
    AND TABLE_SCHEMA = @p_Schema
  FOR XML PATH('Table')
