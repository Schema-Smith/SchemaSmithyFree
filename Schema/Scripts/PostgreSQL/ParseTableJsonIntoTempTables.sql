-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

    DROP TABLE IF EXISTS temp_tables;

    -- "_RowId" gives each parsed row a unique identifier so the per-row ShouldApply DELETE
    -- below targets exactly the source row whose expression evaluated false. Without it,
    -- the DELETE matched on ("Schema", "Name") and would silently wipe both rows when two
    -- entries shared a name with mutually exclusive ShouldApply expressions.
    CREATE TEMPORARY TABLE temp_tables AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT ROW_NUMBER() OVER () AS "_RowId",
           elem ->> 'Schema' AS "Schema",
           elem ->> 'Name' AS "Name",
           COALESCE(elem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(elem ->> 'VariantName', '') AS "VariantName",
           COALESCE(elem ->> 'OldName', '') AS "OldName",
           COALESCE((elem ->> 'RowLevelSecurity')::BOOLEAN, false) AS "RowLevelSecurity",
           COALESCE((elem ->> 'ForceRowLevelSecurity')::BOOLEAN, false) AS "ForceRowLevelSecurity",
           COALESCE(elem ->> 'AccessMethod', '') AS "AccessMethod",
           COALESCE(elem ->> 'PersistenceType', '') AS "PersistenceType",
           CASE WHEN p_UpdateFillFactor THEN true ELSE COALESCE((elem ->> 'UpdateFillFactor')::BOOLEAN, false) END AS "UpdateFillFactor",
           COALESCE(NULLIF((elem ->> 'FillFactor')::INT2, 0), 100) AS "FillFactor"
    FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem;

    -- ShouldApply scoped by "_RowId" so each generated DELETE targets exactly the source row.
    SELECT STRING_AGG('DELETE FROM temp_tables WHERE "_RowId" = ' || "_RowId"::TEXT || ' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_tables
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    DROP TABLE IF EXISTS temp_columns;
    CREATE TEMPORARY TABLE temp_columns AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT ROW_NUMBER() OVER () AS "_RowId",
           elem ->> 'Schema' AS "TableSchema",
           elem ->> 'Name' AS "TableName",
           celem ->> 'Name' AS "Name",
           COALESCE(celem ->> 'DataType', '') AS "DataType",
           COALESCE((celem ->> 'Nullable')::BOOLEAN, false) AS "Nullable",
           COALESCE(celem ->> 'Default', '') AS "Default",
           COALESCE(celem ->> 'Collation', '') AS "Collation",
           COALESCE(celem ->> 'Generated', 'NEVER') AS "Generated",
           COALESCE(celem ->> 'GenerationExpression', '') AS "GenerationExpression",
           COALESCE((celem ->> 'Virtual')::BOOLEAN, false) AS "Virtual",
           COALESCE(celem ->> 'Storage', '') AS "Storage",
           COALESCE(celem ->> 'Compression', '') AS "Compression",
           COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(celem ->> 'VariantName', '') AS "VariantName",
           COALESCE(celem ->> 'OldName', '') AS "OldName"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
      CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS((elem ->> 'Columns')::JSON) AS celem(value);

    -- Synonym mapping
    UPDATE temp_columns
       SET "DataType" = CASE WHEN TRIM(UPPER("DataType")) = 'BIGINT' THEN 'INT8'
                             WHEN TRIM(UPPER("DataType")) = 'BIGSERIAL' THEN 'SERIAL8'
                             WHEN TRIM(UPPER("DataType")) = 'BOOLEAN' THEN 'BOOL'
                             WHEN TRIM(UPPER("DataType")) = 'DOUBLE PRECISION' THEN 'FLOAT8'
                             WHEN TRIM(UPPER("DataType")) = 'FLOAT' THEN 'FLOAT8'
                             WHEN TRIM(UPPER("DataType")) = 'INTEGER' THEN 'INT4'
                             WHEN TRIM(UPPER("DataType")) = 'INT' THEN 'INT4'
                             WHEN TRIM(UPPER("DataType")) = 'REAL' THEN 'FLOAT4'
                             WHEN TRIM(UPPER("DataType")) = 'SMALLINT' THEN 'INT2'
                             WHEN TRIM(UPPER("DataType")) = 'SMALLSERIAL' THEN 'SERIAL2'
                             WHEN TRIM(UPPER("DataType")) = 'SERIAL' THEN 'SERIAL4'
                             WHEN "DataType" ILIKE 'bit varying%' THEN REGEXP_REPLACE("DataType", 'bit varying', 'VARBIT', 'i')
                             WHEN "DataType" ILIKE 'character varying%' THEN REGEXP_REPLACE("DataType", 'character varying', 'VARCHAR', 'i')
                             WHEN "DataType" ILIKE 'character%' THEN REGEXP_REPLACE("DataType", 'character', 'CHAR', 'i')
                             WHEN "DataType" ILIKE 'decimal%' THEN REGEXP_REPLACE("DataType", 'decimal', 'NUMERIC', 'i')
                             WHEN "DataType" ILIKE 'bpchar%' THEN REGEXP_REPLACE("DataType", 'bpchar', 'CHAR', 'i')
                             ELSE "DataType" END;

    SELECT STRING_AGG('DELETE FROM temp_columns WHERE "_RowId" = ' || "_RowId"::TEXT || ' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_columns
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    DROP TABLE IF EXISTS temp_indexes;
    CREATE TEMPORARY TABLE temp_indexes AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT ROW_NUMBER() OVER () AS "_RowId",
           elem ->> 'Schema' AS "TableSchema",
           elem ->> 'Name' AS "TableName",
           celem ->> 'Name' AS "Name",
           COALESCE((celem ->> 'PrimaryKey')::BOOLEAN, false) AS "PrimaryKey",
           COALESCE((celem ->> 'Unique')::BOOLEAN, false) AS "Unique",
           COALESCE((celem ->> 'UniqueConstraint')::BOOLEAN, false) AS "UniqueConstraint",
           COALESCE((celem ->> 'Clustered')::BOOLEAN, false) AS "Clustered",
           COALESCE(celem ->> 'IndexColumns', '') AS "IndexColumns",
           COALESCE(celem ->> 'IncludeColumns', '') AS "IncludeColumns",
           COALESCE(celem ->> 'AccessMethod', 'btree') AS "AccessMethod",
           COALESCE(celem ->> 'FilterExpression', '') AS "FilterExpression",
           COALESCE((celem ->> 'Deferrable')::BOOLEAN, false) AS "Deferrable",
           COALESCE((celem ->> 'InitiallyDeferred')::BOOLEAN, false) AS "InitiallyDeferred",
           COALESCE((celem ->> 'NullsNotDistinct')::BOOLEAN, false) AS "NullsNotDistinct",
           COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(celem ->> 'VariantName', '') AS "VariantName",
           CASE WHEN p_UpdateFillFactor THEN true ELSE COALESCE((celem ->> 'UpdateFillFactor')::BOOLEAN, false) END AS "UpdateFillFactor",
           COALESCE(NULLIF((celem ->> 'FillFactor')::INT2, 0), 90) AS "FillFactor"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
      CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS((elem ->> 'Indexes')::JSON) AS celem(value);

    UPDATE temp_indexes -- Table-level setting overrides index-level setting when true
      SET "UpdateFillFactor" = true
      WHERE NOT "UpdateFillFactor"
        AND EXISTS (SELECT *
                      FROM temp_tables t
                      WHERE t."Schema" = "TableSchema"
                        AND t."Name" = "TableName"
                        AND t."UpdateFillFactor" = true);

    SELECT STRING_AGG('DELETE FROM temp_indexes WHERE "_RowId" = ' || "_RowId"::TEXT || ' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_indexes
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    DROP TABLE IF EXISTS temp_checks;
    CREATE TEMPORARY TABLE temp_checks AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT ROW_NUMBER() OVER () AS "_RowId",
           elem ->> 'Schema' AS "TableSchema",
           elem ->> 'Name' AS "TableName",
           celem ->> 'Name' AS "Name",
           COALESCE(celem ->> 'Expression', '') AS "Expression",
           COALESCE((celem ->> 'Deferrable')::BOOLEAN, false) AS "Deferrable",
           COALESCE((celem ->> 'InitiallyDeferred')::BOOLEAN, false) AS "InitiallyDeferred",
           COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(celem ->> 'VariantName', '') AS "VariantName"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
      CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS((elem ->> 'CheckConstraints')::JSON) AS celem(value);

    SELECT STRING_AGG('DELETE FROM temp_checks WHERE "_RowId" = ' || "_RowId"::TEXT || ' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_checks
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    DROP TABLE IF EXISTS temp_fks;
    CREATE TEMPORARY TABLE temp_fks AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT ROW_NUMBER() OVER () AS "_RowId",
           elem ->> 'Schema' AS "TableSchema",
           elem ->> 'Name' AS "TableName",
           celem ->> 'Name' AS "Name",
           celem ->> 'Columns' AS "Columns",
           celem ->> 'RelatedTableSchema' AS "RelatedTableSchema",
           celem ->> 'RelatedTable' AS "RelatedTable",
           celem ->> 'RelatedColumns' AS "RelatedColumns",
           COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(celem ->> 'VariantName', '') AS "VariantName",
           COALESCE(celem ->> 'DeleteAction', '') AS "DeleteAction",
           COALESCE(celem ->> 'UpdateAction', '') AS "UpdateAction",
           COALESCE((celem ->> 'Deferrable')::BOOLEAN, false) AS "Deferrable",
           COALESCE((celem ->> 'InitiallyDeferred')::BOOLEAN, false) AS "InitiallyDeferred",
           celem ->> 'MatchType' AS "MatchType"
    FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
      CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS((elem ->> 'ForeignKeys')::JSON) AS celem(value);

    SELECT STRING_AGG('DELETE FROM temp_fks WHERE "_RowId" = ' || "_RowId"::TEXT || ' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_fks
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    DROP TABLE IF EXISTS temp_statistics;
    CREATE TEMPORARY TABLE temp_statistics AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT ROW_NUMBER() OVER () AS "_RowId",
           elem ->> 'Schema' AS "TableSchema",
           elem ->> 'Name' AS "TableName",
           celem ->> 'Name' AS "Name",
           COALESCE(celem ->> 'Kind', '') AS "Kind",
           COALESCE(celem ->> 'StatisticsColumns', '') AS "StatisticsColumns",
           COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(celem ->> 'VariantName', '') AS "VariantName"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
      CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS((elem ->> 'Statistics')::JSON) AS celem(value);

    SELECT STRING_AGG('DELETE FROM temp_statistics WHERE "_RowId" = ' || "_RowId"::TEXT || ' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_statistics
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    DROP TABLE IF EXISTS temp_excludes;
    CREATE TEMPORARY TABLE temp_excludes AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT ROW_NUMBER() OVER () AS "_RowId",
           elem ->> 'Schema' AS "TableSchema",
           elem ->> 'Name' AS "TableName",
           celem ->> 'Name' AS "Name",
           (celem ->> 'ExcludeColumns')::JSON AS "ExcludeColumns",
           COALESCE(celem ->> 'AccessMethod', '') AS "AccessMethod",
           COALESCE(celem ->> 'FilterExpression', '') AS "FilterExpression",
           COALESCE((celem ->> 'Deferrable')::BOOLEAN, false) AS "Deferrable",
           COALESCE((celem ->> 'InitiallyDeferred')::BOOLEAN, false) AS "InitiallyDeferred",
           COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(celem ->> 'VariantName', '') AS "VariantName"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
      CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS((elem ->> 'ExcludeConstraints')::JSON) AS celem(value);

    SELECT STRING_AGG('DELETE FROM temp_excludes WHERE "_RowId" = ' || "_RowId"::TEXT || ' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_excludes
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);
