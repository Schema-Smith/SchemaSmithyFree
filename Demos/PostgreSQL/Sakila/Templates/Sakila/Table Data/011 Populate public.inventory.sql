
DO $$
DECLARE
  v_json JSON = '{{public.inventory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."inventory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'film_id')::int4 AS "film_id",
           (elem ->> 'inventory_id')::int4 AS "inventory_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'store_id')::int4 AS "store_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."inventory_id" = "Target"."inventory_id"

WHEN MATCHED AND (NOT ("Target"."film_id" = "Source"."film_id" OR ("Target"."film_id" IS NULL AND "Source"."film_id" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."store_id" = "Source"."store_id" OR ("Target"."store_id" IS NULL AND "Source"."store_id" IS NULL))) THEN
  UPDATE SET
        "film_id" = "Source"."film_id",
        "last_update" = "Source"."last_update",
        "store_id" = "Source"."store_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "film_id",
        "inventory_id",
        "last_update",
        "store_id"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."film_id",
        "Source"."inventory_id",
        "Source"."last_update",
        "Source"."store_id"
   )
 ;

SELECT SETVAL('inventory_inventory_id_seq', (SELECT MAX("inventory_id") FROM "public"."inventory")) INTO nextval;

END $$ LANGUAGE plpgsql;
