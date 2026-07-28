-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

-- Insert-only population written as INSERT ... ON CONFLICT DO NOTHING so it runs on every supported
-- PostgreSQL version (MERGE is a v15 feature; the supported floor is 14).
DO $$
DECLARE
  v_json JSON = '{{TestTableData}}';
BEGIN
INSERT INTO "public"."TestTable" AS "Target" (
  "DateCreated",
  "SomeText",
  "ParentID",
  "TestID",
  "Status"
)
SELECT "Source"."DateCreated",
       "Source"."SomeText",
       "Source"."ParentID",
       "Source"."TestID",
       "Source"."Status"
  FROM (WITH my_tables(arr) AS (VALUES(v_json::JSON))
        SELECT (elem ->> 'DateCreated')::timestamp AS "DateCreated",
               (elem ->> 'SomeText')::varchar(2000) AS "SomeText",
               (elem ->> 'ParentID')::uuid AS "ParentID",
               (elem ->> 'TestID')::uuid AS "TestID",
               (elem ->> 'Status')::smallint AS "Status"
          FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem) AS "Source"
ON CONFLICT ("TestID") DO NOTHING;
END $$ LANGUAGE plpgsql;
