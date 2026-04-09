
DO $$
DECLARE
  v_json JSON = '{{public.customer.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."customer" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'active')::int4 AS "active",
           (elem ->> 'activebool')::bool AS "activebool",
           (elem ->> 'address_id')::int4 AS "address_id",
           (elem ->> 'create_date')::date AS "create_date",
           (elem ->> 'customer_id')::int4 AS "customer_id",
           (elem ->> 'email')::varchar(50) AS "email",
           (elem ->> 'first_name')::varchar(45) AS "first_name",
           (elem ->> 'last_name')::varchar(45) AS "last_name",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'store_id')::int4 AS "store_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."customer_id" = "Target"."customer_id"

WHEN MATCHED AND (NOT ("Target"."active" = "Source"."active" OR ("Target"."active" IS NULL AND "Source"."active" IS NULL)) OR NOT ("Target"."activebool" = "Source"."activebool" OR ("Target"."activebool" IS NULL AND "Source"."activebool" IS NULL)) OR NOT ("Target"."address_id" = "Source"."address_id" OR ("Target"."address_id" IS NULL AND "Source"."address_id" IS NULL)) OR NOT ("Target"."create_date" = "Source"."create_date" OR ("Target"."create_date" IS NULL AND "Source"."create_date" IS NULL)) OR NOT ("Target"."email" = "Source"."email" OR ("Target"."email" IS NULL AND "Source"."email" IS NULL)) OR NOT ("Target"."first_name" = "Source"."first_name" OR ("Target"."first_name" IS NULL AND "Source"."first_name" IS NULL)) OR NOT ("Target"."last_name" = "Source"."last_name" OR ("Target"."last_name" IS NULL AND "Source"."last_name" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."store_id" = "Source"."store_id" OR ("Target"."store_id" IS NULL AND "Source"."store_id" IS NULL))) THEN
  UPDATE SET
        "active" = "Source"."active",
        "activebool" = "Source"."activebool",
        "address_id" = "Source"."address_id",
        "create_date" = "Source"."create_date",
        "email" = "Source"."email",
        "first_name" = "Source"."first_name",
        "last_name" = "Source"."last_name",
        "last_update" = "Source"."last_update",
        "store_id" = "Source"."store_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "active",
        "activebool",
        "address_id",
        "create_date",
        "customer_id",
        "email",
        "first_name",
        "last_name",
        "last_update",
        "store_id"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."active",
        "Source"."activebool",
        "Source"."address_id",
        "Source"."create_date",
        "Source"."customer_id",
        "Source"."email",
        "Source"."first_name",
        "Source"."last_name",
        "Source"."last_update",
        "Source"."store_id"
   )
 ;

SELECT SETVAL('customer_customer_id_seq', (SELECT MAX("customer_id") FROM "public"."customer")) INTO nextval;

END $$ LANGUAGE plpgsql;
