
DO $$
DECLARE
  v_json JSON = '{{public.country.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."country" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'country')::varchar(50) AS "country",
           (elem ->> 'country_id')::int4 AS "country_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."country_id" = "Target"."country_id"

WHEN MATCHED AND (NOT ("Target"."country" = "Source"."country" OR ("Target"."country" IS NULL AND "Source"."country" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL))) THEN
  UPDATE SET
        "country" = "Source"."country",
        "last_update" = "Source"."last_update"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "country",
        "country_id",
        "last_update"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."country",
        "Source"."country_id",
        "Source"."last_update"
   )
 ;

SELECT SETVAL('country_country_id_seq', (SELECT MAX("country_id") FROM "public"."country")) INTO nextval;

END $$ LANGUAGE plpgsql;
