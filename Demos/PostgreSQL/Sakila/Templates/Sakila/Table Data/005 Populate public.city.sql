
DO $$
DECLARE
  v_json JSON = '{{public.city.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."city" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'city')::varchar(50) AS "city",
           (elem ->> 'city_id')::int4 AS "city_id",
           (elem ->> 'country_id')::int4 AS "country_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."city_id" = "Target"."city_id"

WHEN MATCHED AND (NOT ("Target"."city" = "Source"."city" OR ("Target"."city" IS NULL AND "Source"."city" IS NULL)) OR NOT ("Target"."country_id" = "Source"."country_id" OR ("Target"."country_id" IS NULL AND "Source"."country_id" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL))) THEN
  UPDATE SET
        "city" = "Source"."city",
        "country_id" = "Source"."country_id",
        "last_update" = "Source"."last_update"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "city",
        "city_id",
        "country_id",
        "last_update"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."city",
        "Source"."city_id",
        "Source"."country_id",
        "Source"."last_update"
   )
 ;

SELECT SETVAL('city_city_id_seq', (SELECT MAX("city_id") FROM "public"."city")) INTO nextval;

END $$ LANGUAGE plpgsql;
