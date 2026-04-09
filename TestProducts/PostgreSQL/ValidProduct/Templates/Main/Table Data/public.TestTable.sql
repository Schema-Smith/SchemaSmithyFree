-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

DO $$
DECLARE
  v_json JSON = '{{TestTableInitialData}}';
BEGIN

MERGE INTO "public"."TestTable" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'DateCreated')::timestamp AS "DateCreated",
           (elem ->> 'SomeText')::varchar(2000) AS "SomeText",
           (elem ->> 'ParentID')::uuid AS "ParentID",
           (elem ->> 'TestID')::uuid AS "TestID",
           (elem ->> 'Status')::smallint AS "Status"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."TestID" = "Target"."TestID"

 WHEN NOT MATCHED THEN -- BY TARGET omitted for legacy version support
   INSERT (
         "DateCreated",
        "SomeText",
        "ParentID",
        "TestID",
        "Status"
   )
  VALUES (
         "Source"."DateCreated",
        "Source"."SomeText",
        "Source"."ParentID",
        "Source"."TestID",
        "Source"."Status"
   )
 ;

END $$ LANGUAGE plpgsql;
