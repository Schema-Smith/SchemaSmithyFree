
DO $$
DECLARE
  v_json JSON = '{{public.us_states.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."us_states" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'state_abbr')::varchar(2) AS "state_abbr",
           (elem ->> 'state_id')::int2 AS "state_id",
           (elem ->> 'state_name')::varchar(100) AS "state_name",
           (elem ->> 'state_region')::varchar(50) AS "state_region"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."state_id" = "Target"."state_id"

WHEN MATCHED AND (NOT ("Target"."state_abbr" = "Source"."state_abbr" OR ("Target"."state_abbr" IS NULL AND "Source"."state_abbr" IS NULL)) OR NOT ("Target"."state_id" = "Source"."state_id" OR ("Target"."state_id" IS NULL AND "Source"."state_id" IS NULL)) OR NOT ("Target"."state_name" = "Source"."state_name" OR ("Target"."state_name" IS NULL AND "Source"."state_name" IS NULL)) OR NOT ("Target"."state_region" = "Source"."state_region" OR ("Target"."state_region" IS NULL AND "Source"."state_region" IS NULL))) THEN
  UPDATE SET
        "state_abbr" = "Source"."state_abbr",
        "state_id" = "Source"."state_id",
        "state_name" = "Source"."state_name",
        "state_region" = "Source"."state_region"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "state_abbr",
        "state_id",
        "state_name",
        "state_region"
   ) 
  VALUES (
         "Source"."state_abbr",
        "Source"."state_id",
        "Source"."state_name",
        "Source"."state_region"
   )
 ;



END $$ LANGUAGE plpgsql;
