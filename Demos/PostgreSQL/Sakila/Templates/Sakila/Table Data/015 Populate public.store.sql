
DO $$
DECLARE
  v_json JSON = '{{public.store.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."store" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'address_id')::int4 AS "address_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'manager_staff_id')::int4 AS "manager_staff_id",
           (elem ->> 'store_id')::int4 AS "store_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."store_id" = "Target"."store_id"

WHEN MATCHED AND (NOT ("Target"."address_id" = "Source"."address_id" OR ("Target"."address_id" IS NULL AND "Source"."address_id" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."manager_staff_id" = "Source"."manager_staff_id" OR ("Target"."manager_staff_id" IS NULL AND "Source"."manager_staff_id" IS NULL))) THEN
  UPDATE SET
        "address_id" = "Source"."address_id",
        "last_update" = "Source"."last_update",
        "manager_staff_id" = "Source"."manager_staff_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "address_id",
        "last_update",
        "manager_staff_id",
        "store_id"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."address_id",
        "Source"."last_update",
        "Source"."manager_staff_id",
        "Source"."store_id"
   )
 ;

SELECT SETVAL('store_store_id_seq', (SELECT MAX("store_id") FROM "public"."store")) INTO nextval;

END $$ LANGUAGE plpgsql;
