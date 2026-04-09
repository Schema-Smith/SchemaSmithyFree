
DO $$
DECLARE
  v_json JSON = '{{public.categories.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."categories" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'category_id')::int2 AS "category_id",
           (elem ->> 'category_name')::varchar(15) AS "category_name",
           (elem ->> 'description')::text AS "description",
           decode(elem ->> 'picture', 'base64') AS "picture"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."category_id" = "Target"."category_id"

WHEN MATCHED AND (NOT ("Target"."category_id" = "Source"."category_id" OR ("Target"."category_id" IS NULL AND "Source"."category_id" IS NULL)) OR NOT ("Target"."category_name" = "Source"."category_name" OR ("Target"."category_name" IS NULL AND "Source"."category_name" IS NULL)) OR NOT ("Target"."description" = "Source"."description" OR ("Target"."description" IS NULL AND "Source"."description" IS NULL)) OR NOT ("Target"."picture" = "Source"."picture" OR ("Target"."picture" IS NULL AND "Source"."picture" IS NULL))) THEN
  UPDATE SET
        "category_id" = "Source"."category_id",
        "category_name" = "Source"."category_name",
        "description" = "Source"."description",
        "picture" = "Source"."picture"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "category_id",
        "category_name",
        "description",
        "picture"
   ) 
  VALUES (
         "Source"."category_id",
        "Source"."category_name",
        "Source"."description",
        "Source"."picture"
   )
 ;



END $$ LANGUAGE plpgsql;
