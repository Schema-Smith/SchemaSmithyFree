
DO $$
DECLARE
  v_json JSON = '{{public.category.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."category" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'category_id')::int4 AS "category_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'name')::varchar(25) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."category_id" = "Target"."category_id"

WHEN MATCHED AND (NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "last_update" = "Source"."last_update",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "category_id",
        "last_update",
        "name"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."category_id",
        "Source"."last_update",
        "Source"."name"
   )
 ;

SELECT SETVAL('category_category_id_seq', (SELECT MAX("category_id") FROM "public"."category")) INTO nextval;

END $$ LANGUAGE plpgsql;
