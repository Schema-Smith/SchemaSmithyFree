
DO $$
DECLARE
  v_json JSON = '{{public.film_category.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."film_category" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'category_id')::int4 AS "category_id",
           (elem ->> 'film_id')::int4 AS "film_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."film_id" = "Target"."film_id" AND "Source"."category_id" = "Target"."category_id"

WHEN MATCHED AND (NOT ("Target"."category_id" = "Source"."category_id" OR ("Target"."category_id" IS NULL AND "Source"."category_id" IS NULL)) OR NOT ("Target"."film_id" = "Source"."film_id" OR ("Target"."film_id" IS NULL AND "Source"."film_id" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL))) THEN
  UPDATE SET
        "category_id" = "Source"."category_id",
        "film_id" = "Source"."film_id",
        "last_update" = "Source"."last_update"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "category_id",
        "film_id",
        "last_update"
   ) 
  VALUES (
         "Source"."category_id",
        "Source"."film_id",
        "Source"."last_update"
   )
 ;



END $$ LANGUAGE plpgsql;
