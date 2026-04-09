
DO $$
DECLARE
  v_json JSON = '{{public.territories.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."territories" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'region_id')::int2 AS "region_id",
           (elem ->> 'territory_description')::varchar(60) AS "territory_description",
           (elem ->> 'territory_id')::varchar(20) AS "territory_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."territory_id" = "Target"."territory_id"

WHEN MATCHED AND (NOT ("Target"."region_id" = "Source"."region_id" OR ("Target"."region_id" IS NULL AND "Source"."region_id" IS NULL)) OR NOT ("Target"."territory_description" = "Source"."territory_description" OR ("Target"."territory_description" IS NULL AND "Source"."territory_description" IS NULL)) OR NOT ("Target"."territory_id" = "Source"."territory_id" OR ("Target"."territory_id" IS NULL AND "Source"."territory_id" IS NULL))) THEN
  UPDATE SET
        "region_id" = "Source"."region_id",
        "territory_description" = "Source"."territory_description",
        "territory_id" = "Source"."territory_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "region_id",
        "territory_description",
        "territory_id"
   ) 
  VALUES (
         "Source"."region_id",
        "Source"."territory_description",
        "Source"."territory_id"
   )
 ;



END $$ LANGUAGE plpgsql;
