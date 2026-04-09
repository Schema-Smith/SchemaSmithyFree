
DO $$
DECLARE
  v_json JSON = '{{public.region.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."region" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'region_description')::varchar(60) AS "region_description",
           (elem ->> 'region_id')::int2 AS "region_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."region_id" = "Target"."region_id"

WHEN MATCHED AND (NOT ("Target"."region_description" = "Source"."region_description" OR ("Target"."region_description" IS NULL AND "Source"."region_description" IS NULL)) OR NOT ("Target"."region_id" = "Source"."region_id" OR ("Target"."region_id" IS NULL AND "Source"."region_id" IS NULL))) THEN
  UPDATE SET
        "region_description" = "Source"."region_description",
        "region_id" = "Source"."region_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "region_description",
        "region_id"
   ) 
  VALUES (
         "Source"."region_description",
        "Source"."region_id"
   )
 ;



END $$ LANGUAGE plpgsql;
