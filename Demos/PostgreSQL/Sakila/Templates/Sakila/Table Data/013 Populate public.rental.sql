
DO $$
DECLARE
  v_json JSON = '{{public.rental.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."rental" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'customer_id')::int4 AS "customer_id",
           (elem ->> 'inventory_id')::int4 AS "inventory_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'rental_date')::timestamp(6) AS "rental_date",
           (elem ->> 'rental_id')::int4 AS "rental_id",
           (elem ->> 'return_date')::timestamp(6) AS "return_date",
           (elem ->> 'staff_id')::int4 AS "staff_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."rental_id" = "Target"."rental_id"

WHEN MATCHED AND (NOT ("Target"."customer_id" = "Source"."customer_id" OR ("Target"."customer_id" IS NULL AND "Source"."customer_id" IS NULL)) OR NOT ("Target"."inventory_id" = "Source"."inventory_id" OR ("Target"."inventory_id" IS NULL AND "Source"."inventory_id" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."rental_date" = "Source"."rental_date" OR ("Target"."rental_date" IS NULL AND "Source"."rental_date" IS NULL)) OR NOT ("Target"."return_date" = "Source"."return_date" OR ("Target"."return_date" IS NULL AND "Source"."return_date" IS NULL)) OR NOT ("Target"."staff_id" = "Source"."staff_id" OR ("Target"."staff_id" IS NULL AND "Source"."staff_id" IS NULL))) THEN
  UPDATE SET
        "customer_id" = "Source"."customer_id",
        "inventory_id" = "Source"."inventory_id",
        "last_update" = "Source"."last_update",
        "rental_date" = "Source"."rental_date",
        "return_date" = "Source"."return_date",
        "staff_id" = "Source"."staff_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "customer_id",
        "inventory_id",
        "last_update",
        "rental_date",
        "rental_id",
        "return_date",
        "staff_id"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."customer_id",
        "Source"."inventory_id",
        "Source"."last_update",
        "Source"."rental_date",
        "Source"."rental_id",
        "Source"."return_date",
        "Source"."staff_id"
   )
 ;

SELECT SETVAL('rental_rental_id_seq', (SELECT MAX("rental_id") FROM "public"."rental")) INTO nextval;

END $$ LANGUAGE plpgsql;
