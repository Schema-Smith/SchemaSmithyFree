-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

CREATE OR REPLACE PROCEDURE "SchemaSmith"."BuildExistingIndexesSnapshot"()
    LANGUAGE plpgsql
AS $$
BEGIN
  -- Session-scoped snapshot of existing indexes, consumed by ModifiedTableQuench,
  -- IndexOnlyQuench, and MissingIndexesAndConstraintsQuench. Extracted to one proc so the
  -- three sites can never drift and so a checkpoint-resumed run (which skips the step that
  -- normally builds it) can rebuild it on demand (#332). Depends only on temp_tables.
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
           CASE WHEN con.contype = 'u' THEN TRUE ELSE FALSE END AS "UniqueConstraint",
           idx.indisprimary AS "PrimaryKey",
           idx.indisclustered AS "Clustered",
           COALESCE(PG_GET_EXPR(idx.indpred, idx.indrelid), '') AS "FilterExpression",
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
      LEFT JOIN pg_catalog.pg_constraint con ON con.contype = 'u' AND con.conrelid = idx.indrelid AND con.conname = i.relname;
END $$;
