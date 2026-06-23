-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."IndexOnlyQuench"
(p_ProductName VARCHAR(50),
 p_TableDefinitions TEXT,
 p_WhatIf BOOLEAN = FALSE,
 p_DropUnknownIndexes BOOLEAN = FALSE,
 p_UpdateFillFactor BOOLEAN = TRUE)
    LANGUAGE plpgsql
AS $$
DECLARE
  table_json TEXT = CASE WHEN LEFT(p_TableDefinitions, 1) = '[' THEN p_TableDefinitions ELSE '[' || p_TableDefinitions || ']' END;
  sql_script TEXT = '';
BEGIN
    CREATE TEMPORARY TABLE temp_tables AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT elem ->> 'Schema' AS "Schema",
           elem ->> 'Name' AS "Name",
           COALESCE(elem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(elem ->> 'OldName', '') AS "OldName",
           COALESCE((elem ->> 'RowLevelSecurity')::BOOLEAN, false) AS "RowLevelSecurity",
           COALESCE((elem ->> 'ForceRowLevelSecurity')::BOOLEAN, false) AS "ForceRowLevelSecurity",
           COALESCE(elem ->> 'AccessMethod', '') AS "AccessMethod",
           COALESCE(elem ->> 'PersistenceType', '') AS "PersistenceType",
           CASE WHEN p_UpdateFillFactor THEN true ELSE COALESCE((elem ->> 'UpdateFillFactor')::BOOLEAN, false) END AS "UpdateFillFactor"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem;

    SELECT STRING_AGG('DELETE FROM temp_tables WHERE "Schema" = ''' || "Schema" || ''' AND "Name" = ''' || "Name" || ''' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_tables
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    DROP TABLE IF EXISTS temp_indexes;
    CREATE TEMPORARY TABLE temp_indexes AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT COALESCE(elem ->> 'Schema', '') AS "TableSchema",
           COALESCE(elem ->> 'Name', '') AS "TableName",
           COALESCE(celem ->> 'Name', '') AS "Name",
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

    SELECT STRING_AGG('DELETE FROM temp_indexes WHERE "TableSchema" = ''' || "TableSchema" || ''' AND "TableName" = ''' || "TableName" || ''' AND "Name" = ''' || "Name" || ''' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_indexes
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    
    DROP TABLE IF EXISTS temp_statistics;
    CREATE TEMPORARY TABLE temp_statistics AS
    WITH my_tables(arr) AS (VALUES(table_json::JSON))
    SELECT elem ->> 'Schema' AS "TableSchema",
           elem ->> 'Name' AS "TableName",
           celem ->> 'Name' AS "Name",
           COALESCE(celem ->> 'Kind', '') AS "Kind",
           COALESCE(celem ->> 'StatisticsColumns', '') AS "StatisticsColumns",
           COALESCE(celem ->> 'ShouldApplyExpression', '') AS "ShouldApplyExpression",
           COALESCE(celem ->> 'VariantName', '') AS "VariantName"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
      CROSS JOIN LATERAL JSON_ARRAY_ELEMENTS((elem ->> 'Statistics')::JSON) AS celem(value);

    SELECT STRING_AGG('DELETE FROM temp_statistics WHERE "TableSchema" = ''' || "TableSchema" || ''' AND "TableName" = ''' || "TableName" || ''' AND "Name" = ''' || "Name" || ''' AND NOT (' || "ShouldApplyExpression" || ');', CHR(10))
      INTO sql_script
      FROM temp_statistics
      WHERE NULLIF("ShouldApplyExpression", '') IS NOT NULL;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, false);

    RAISE NOTICE 'Collect Existing Indexes';
    DROP TABLE IF EXISTS temp_existing_indexes;
    CREATE TEMPORARY TABLE temp_existing_indexes AS
      SELECT t."Schema" AS "TableSchema",
             t."Name" AS "TableName",
             i.relname AS "IndexName",
             (SELECT STRING_AGG(a.attname || CASE WHEN (idx.indoption[idx] & 1) = 1 THEN ' DESC' ELSE '' END, ',' ORDER BY idx)
                FROM pg_attribute a
                CROSS JOIN LATERAL UNNEST(idx.indkey) WITH ORDINALITY AS u(element, idx)
                WHERE a.attrelid = idx.indrelid
                  AND idx <= idx.indnkeyatts
                  AND a.attnum = element) AS "IndexColumns",
             (SELECT STRING_AGG(a.attname, ',' ORDER BY idx)
                FROM pg_attribute a
                CROSS JOIN LATERAL UNNEST(idx.indkey) WITH ORDINALITY AS u(element, idx)
                WHERE a.attrelid = idx.indrelid
                  AND idx > idx.indnkeyatts
                  AND a.attnum = element) AS "IncludeColumns",
             idx.indisunique AS "Unique",
             CAST(CASE WHEN EXISTS (SELECT * FROM pg_catalog.pg_constraint con WHERE con.contype = 'u' AND con.conrelid = idx.indrelid AND con.conname = i.relname)
                       THEN TRUE ELSE FALSE END AS BOOLEAN) AS "UniqueConstraint",
             idx.indisprimary AS "PrimaryKey",
             idx.indisclustered AS "Clustered",
             PG_GET_EXPR(idx.indpred, idx.indrelid) AS "FilterExpression",
             (SELECT am.amname FROM pg_am am WHERE i.relam = am.oid AND i.relkind = 'i') AS "AccessMethod",
             CASE WHEN 'fillfactor=100' = ANY(i.reloptions) THEN 100
                  WHEN i.reloptions IS NULL THEN 90 -- Default for B-tree indexes
                  ELSE (regexp_match(array_to_string(i.reloptions, ','), 'fillfactor=(\d+)') ) [1] ::int
                  END AS "FillFactor",
             idx.indnullsnotdistinct AS "NullsNotDistinct",
             COALESCE(con.condeferrable, FALSE) AS "Deferrable",
             COALESCE(con.condeferred, FALSE) AS "InitiallyDeferred"
        FROM temp_tables t
        JOIN pg_index idx ON idx.indrelid = ('"' || t."Schema" || '"' ||  '.' || '"' ||  t."Name" || '"')::regclass
        JOIN pg_class i ON i.oid = idx.indexrelid
        LEFT JOIN pg_catalog.pg_constraint con ON con.conrelid = idx.indrelid AND con.conname = i.relname AND con.contype IN ('p', 'u');

    RAISE NOTICE 'Handle Renamed Indexes And Unique Constraints';
    SELECT STRING_AGG('RAISE NOTICE ''  Renaming ' || CASE WHEN ei."PrimaryKey" OR ei."UniqueConstraint" THEN 'Constraint' ELSE 'Index' END || ' ' || ei."TableSchema" || '.' || ei."TableName" || '.' || ei."IndexName" || ' to ' || i."Name" || ''';' || CHR(10) ||
                      CASE WHEN NOT (ei."PrimaryKey" OR ei."UniqueConstraint")
                           THEN 'ALTER INDEX IF EXISTS "' || ei."TableSchema" || '"."' || ei."IndexName" || '" RENAME TO "' || i."Name" || '";'
                           ELSE 'ALTER TABLE "' || ei."TableSchema" || '"."' || ei."TableName" || '" RENAME CONSTRAINT "' || ei."IndexName" || '" TO "' || i."Name" || '";' END, CHR(10))
      INTO sql_script
      FROM temp_existing_indexes ei
      JOIN temp_indexes i ON i."TableSchema" = ei."TableSchema"
                         AND i."TableName" = ei."TableName"
                         AND i."Name" != ei."IndexName"
                         AND i."IndexColumns" = ei."IndexColumns"
                         AND COALESCE(i."IncludeColumns", '') = COALESCE(ei."IncludeColumns", '')
                         AND COALESCE(i."Unique", FALSE) = ei."Unique"
                         AND COALESCE(i."UniqueConstraint", FALSE) = ei."UniqueConstraint"
                         AND COALESCE(i."PrimaryKey", FALSE) = ei."PrimaryKey"
                         AND COALESCE(i."FilterExpression", '') = COALESCE(ei."FilterExpression", '')
                         AND COALESCE(i."AccessMethod", 'btree') = COALESCE(ei."AccessMethod", 'btree')
                         AND COALESCE(i."NullsNotDistinct", false) = COALESCE(ei."NullsNotDistinct", false)
                         AND COALESCE(i."Deferrable", false) = COALESCE(ei."Deferrable", false)
                         AND COALESCE(i."InitiallyDeferred", false) = COALESCE(ei."InitiallyDeferred", false)
      WHERE NOT EXISTS (SELECT 1
                          FROM temp_indexes i
                          WHERE i."TableSchema" = ei."TableSchema"
                            AND i."TableName" = ei."TableName"
                            AND i."Name" = ei."IndexName");
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Identify Unknown, Removed, and Modified Indexes to Drop';
    DROP TABLE IF EXISTS temp_indexes_to_drop;
    CREATE TEMPORARY TABLE temp_indexes_to_drop AS
      SELECT ei."TableSchema",
             ei."TableName",
             ei."IndexName",
             ei."PrimaryKey" OR ei."UniqueConstraint" AS "IsConstraint"
        FROM temp_existing_indexes ei
        WHERE (p_DropUnknownIndexes 
           AND NOT EXISTS (SELECT 1 -- Unknown Index
                             FROM temp_indexes i
                             WHERE i."TableSchema" = ei."TableSchema"
                               AND i."TableName" = ei."TableName"
                               AND i."Name" = ei."IndexName"))
           OR EXISTS (SELECT 1 -- Modified Index
                        FROM temp_indexes i
                        WHERE i."TableSchema" = ei."TableSchema"
                          AND i."TableName" = ei."TableName"
                          AND i."Name" = ei."IndexName"
                          AND (i."IndexColumns" != ei."IndexColumns"
                            OR COALESCE(i."IncludeColumns", '') != COALESCE(ei."IncludeColumns", '')
                            OR (COALESCE(i."Unique", FALSE) OR COALESCE(i."PrimaryKey", FALSE) OR COALESCE(i."UniqueConstraint", FALSE)) != ei."Unique"
                            OR COALESCE(i."UniqueConstraint", FALSE) != ei."UniqueConstraint"
                            OR COALESCE(i."PrimaryKey", FALSE) != ei."PrimaryKey"
                            OR COALESCE(i."FilterExpression", '') != COALESCE(ei."FilterExpression", '')
                            OR COALESCE(i."AccessMethod", 'btree') != COALESCE(ei."AccessMethod", 'btree'))
                            OR COALESCE(i."NullsNotDistinct", false) != COALESCE(ei."NullsNotDistinct", false)
                            OR COALESCE(i."Deferrable", false) != COALESCE(ei."Deferrable", false)
                            OR COALESCE(i."InitiallyDeferred", false) != COALESCE(ei."InitiallyDeferred", false))
           OR EXISTS (SELECT 1 -- Index Removed from Product Definition
                        FROM "SchemaSmith"."ProductOwnership" tp
                        WHERE tp."ProductName" = p_ProductName
                          AND tp."IndexName" = ei."IndexName"
                          AND tp."Schema" = ei."TableSchema"
                          AND tp."TableName" = ei."TableName"
                          AND NOT EXISTS (SELECT 1
                                            FROM temp_indexes i
                                            WHERE i."TableSchema" = ei."TableSchema"
                                              AND i."TableName" = ei."TableName"
                                              AND i."Name" = ei."IndexName"));

    RAISE NOTICE 'Drop Unknown, Removed, and Modified Indexes';
    SELECT STRING_AGG('RAISE NOTICE ''  Dropping ' || CASE WHEN "IsConstraint" THEN 'Constraint' ELSE 'Index' END || ' ' || ti."TableSchema" || '.' || ti."TableName" || '.' || ti."IndexName" || ''';' || CHR(10) ||
                      CASE WHEN "IsConstraint"
                           THEN 'ALTER TABLE "' || ti."TableSchema" || '"."' || ti."TableName" || '" DROP CONSTRAINT IF EXISTS "' || ti."IndexName" || '" CASCADE;'
                           ELSE 'DROP INDEX IF EXISTS "' || ti."TableSchema" || '"."' || ti."IndexName" || '";' END, CHR(10))
      INTO sql_script
      FROM temp_indexes_to_drop ti;
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Fixup Any Modified Index Fill Factors';
    SELECT STRING_AGG('RAISE NOTICE ''  Modify Fillfactor for ' || ti."TableSchema" || '.' || ti."Name" || ''';' || CHR(10) ||
                      'ALTER INDEX "' || ti."TableSchema" || '"."' || ti."Name" || '" SET (fillfactor = ' || ti."FillFactor" || ');', CHR(10))
      INTO sql_script
      FROM temp_indexes ti
      JOIN temp_existing_indexes ei ON ei."TableSchema" = ti."TableSchema"
                                   AND ei."TableName" = ti."TableName"
                                   AND ei."IndexName" = ti."Name"
      JOIN pg_index idx ON idx.indrelid = ('"' || ti."TableSchema" || '"' ||  '.' || '"' ||  ti."TableName" || '"')::regclass
      JOIN pg_class i ON i.oid = idx.indexrelid
                     AND i.relname = ti."Name"
      WHERE ti."UpdateFillFactor"
        AND ei."FillFactor" != ti."FillFactor"
        AND COALESCE(ti."AccessMethod", 'btree') NOT IN ('gin', 'brin', 'spgist');
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

    RAISE NOTICE 'Add Missing Indexes'; -- Includes Primary Keys and Unique Constraints
    SELECT STRING_AGG('RAISE NOTICE ''  Add missing ' || CASE WHEN ti."UniqueConstraint" OR ti."PrimaryKey" THEN 'Constraint ' ELSE 'Index ' END || ti."TableSchema" || '.' || ti."TableName" || '.' || ti."Name" || CASE WHEN COALESCE(ti."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(ti."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                      CASE WHEN ti."UniqueConstraint" OR ti."PrimaryKey"
                           THEN 'ALTER TABLE "' || ti."TableSchema" || '"."' || ti."TableName" || '" ADD CONSTRAINT "' || ti."Name" || '" ' ||
                                CASE WHEN ti."PrimaryKey" 
                                     THEN 'PRIMARY KEY ' 
                                     ELSE 'UNIQUE ' || CASE WHEN ti."NullsNotDistinct" THEN 'NULLS NOT DISTINCT ' ELSE '' END
                                     END ||
                                '(' || "SchemaSmith"."QuoteIndexColumnList"(ti."IndexColumns") || ')' ||
                                CASE WHEN ti."Deferrable" THEN ' DEFERRABLE' ELSE '' END ||
                                CASE WHEN ti."InitiallyDeferred" THEN ' INITIALLY DEFERRED' ELSE '' END ||
                                CASE WHEN COALESCE(ti."AccessMethod", 'btree') NOT IN ('gin', 'brin', 'spgist')
                                     THEN ' WITH (fillfactor = ' || ti."FillFactor" || ')'
                                     ELSE '' END || ';'
                           ELSE 'CREATE ' || CASE WHEN ti."Unique" THEN 'UNIQUE ' ELSE '' END || 'INDEX "' || ti."Name" || '" ON "' || ti."TableSchema" || '"."' || ti."TableName" || '" ' ||
                                'USING ' || COALESCE(ti."AccessMethod", 'btree') || ' ' ||
                                '(' || "SchemaSmith"."QuoteIndexColumnList"(ti."IndexColumns") || ')' ||
                                CASE WHEN NULLIF(ti."IncludeColumns", '') IS NOT NULL THEN ' INCLUDE (' || "SchemaSmith"."QuoteColumnList"(ti."IncludeColumns") || ')' ELSE '' END ||
                                CASE WHEN NULLIF(ti."FilterExpression", '') IS NOT NULL THEN ' WHERE ' || ti."FilterExpression" ELSE '' END ||
                                CASE WHEN COALESCE(ti."AccessMethod", 'btree') NOT IN ('gin', 'brin', 'spgist')
                                     THEN ' WITH (fillfactor = ' || ti."FillFactor" || ')'
                                     ELSE '' END ||
                                CASE WHEN  ti."Unique" AND ti."NullsNotDistinct" THEN ' NULLS NOT DISTINCT' ELSE '' END || ';'
                           END, CHR(10))
      INTO sql_script
      FROM temp_indexes ti
      WHERE NOT EXISTS (SELECT *
                          FROM pg_index idx
                          JOIN pg_class tc ON tc.oid = idx.indrelid
                                          AND tc.relkind = 'r'
                                          AND tc.relname = ti."TableName"
                                          AND tc.relnamespace = (SELECT oid FROM pg_namespace WHERE nspname = ti."TableSchema")
                          JOIN pg_class i ON i.oid = idx.indexrelid
                          WHERE i.relname = ti."Name");
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);


    RAISE NOTICE 'Fixup Table Cluster';
    SELECT STRING_AGG('RAISE NOTICE ''  Fixing up attributes for ' || t."Schema" || '.' || t."Name" || ''';' || CHR(10) ||
                      'ALTER TABLE ' || '"' || t."Schema" || '"."' || t."Name" || '" ' ||
                      CASE WHEN new_clust."NewCluster" IS NOT NULL 
                           THEN 'CLUSTER ON "' || "NewCluster" || '"'
                           ELSE 'SET WITHOUT CLUSTER' END || ';', CHR(10))
      INTO sql_script
      FROM temp_tables t
      LEFT JOIN (SELECT ti."TableSchema", ti."TableName", ti."Name" AS "NewCluster"
                   FROM temp_indexes ti
                   WHERE ti."Clustered") AS new_clust ON new_clust."TableSchema" = t."Schema"
                                                     AND new_clust."TableName" = t."Name"
      LEFT JOIN (SELECT ei."TableSchema", ei."TableName", ei."IndexName" AS "OldCluster"
                   FROM temp_existing_indexes ei
                   WHERE ei."Clustered") AS old_clust ON old_clust."TableSchema" = t."Schema"
                                                     AND old_clust."TableName" = t."Name"
      WHERE COALESCE("NewCluster", '') != COALESCE("OldCluster", '');
    CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

  RAISE NOTICE 'Add Missing Statistics';
  SELECT STRING_AGG('RAISE NOTICE ''  Add missing statistics ' || ts."TableSchema" || '.' || ts."TableName" || '.' || ts."Name" || CASE WHEN COALESCE(ts."VariantName", '') <> '' THEN ' (variant: ' || REPLACE(ts."VariantName", '''', '''''') || ')' ELSE '' END || ''';' || CHR(10) ||
                    'CREATE STATISTICS "' || ts."TableSchema" || '"."' || ts."Name" || '"' ||
                    CASE WHEN NULLIF(TRIM(ts."Kind"), '') IS NOT NULL THEN ' (' || ts."Kind" ||')' ELSE '' END ||
                    ' ON ' || "SchemaSmith"."QuoteIndexColumnList"(ts."StatisticsColumns") ||
                    ' FROM "' || ts."TableSchema" || '"."' || ts."TableName" || '";', CHR(10))
    INTO sql_script
    FROM temp_statistics ts
    WHERE NOT EXISTS (SELECT 1
                        FROM pg_statistic_ext ste
                        JOIN pg_class rel ON rel.oid = ste.stxrelid
                        JOIN pg_namespace nsp ON nsp.oid = rel.relnamespace
                                             AND nsp.nspname = ts."TableSchema"
                                             AND rel.relname = ts."TableName"
                        WHERE ste.stxname = ts."Name");
  CALL "SchemaSmith"."ExecuteOrDebug"(sql_script, p_WhatIf);

END $$;