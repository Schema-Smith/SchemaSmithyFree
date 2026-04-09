
DO $$
DECLARE
  v_json JSON = '{{public.orders.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."orders" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'customer_id')::varchar(5) AS "customer_id",
           (elem ->> 'employee_id')::int2 AS "employee_id",
           (elem ->> 'freight')::float4 AS "freight",
           (elem ->> 'order_date')::date AS "order_date",
           (elem ->> 'order_id')::int2 AS "order_id",
           (elem ->> 'required_date')::date AS "required_date",
           (elem ->> 'ship_address')::varchar(60) AS "ship_address",
           (elem ->> 'ship_city')::varchar(15) AS "ship_city",
           (elem ->> 'ship_country')::varchar(15) AS "ship_country",
           (elem ->> 'ship_name')::varchar(40) AS "ship_name",
           (elem ->> 'ship_postal_code')::varchar(10) AS "ship_postal_code",
           (elem ->> 'ship_region')::varchar(15) AS "ship_region",
           (elem ->> 'ship_via')::int2 AS "ship_via",
           (elem ->> 'shipped_date')::date AS "shipped_date"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."order_id" = "Target"."order_id"

WHEN MATCHED AND (NOT ("Target"."customer_id" = "Source"."customer_id" OR ("Target"."customer_id" IS NULL AND "Source"."customer_id" IS NULL)) OR NOT ("Target"."employee_id" = "Source"."employee_id" OR ("Target"."employee_id" IS NULL AND "Source"."employee_id" IS NULL)) OR NOT ("Target"."freight" = "Source"."freight" OR ("Target"."freight" IS NULL AND "Source"."freight" IS NULL)) OR NOT ("Target"."order_date" = "Source"."order_date" OR ("Target"."order_date" IS NULL AND "Source"."order_date" IS NULL)) OR NOT ("Target"."order_id" = "Source"."order_id" OR ("Target"."order_id" IS NULL AND "Source"."order_id" IS NULL)) OR NOT ("Target"."required_date" = "Source"."required_date" OR ("Target"."required_date" IS NULL AND "Source"."required_date" IS NULL)) OR NOT ("Target"."ship_address" = "Source"."ship_address" OR ("Target"."ship_address" IS NULL AND "Source"."ship_address" IS NULL)) OR NOT ("Target"."ship_city" = "Source"."ship_city" OR ("Target"."ship_city" IS NULL AND "Source"."ship_city" IS NULL)) OR NOT ("Target"."ship_country" = "Source"."ship_country" OR ("Target"."ship_country" IS NULL AND "Source"."ship_country" IS NULL)) OR NOT ("Target"."ship_name" = "Source"."ship_name" OR ("Target"."ship_name" IS NULL AND "Source"."ship_name" IS NULL)) OR NOT ("Target"."ship_postal_code" = "Source"."ship_postal_code" OR ("Target"."ship_postal_code" IS NULL AND "Source"."ship_postal_code" IS NULL)) OR NOT ("Target"."ship_region" = "Source"."ship_region" OR ("Target"."ship_region" IS NULL AND "Source"."ship_region" IS NULL)) OR NOT ("Target"."ship_via" = "Source"."ship_via" OR ("Target"."ship_via" IS NULL AND "Source"."ship_via" IS NULL)) OR NOT ("Target"."shipped_date" = "Source"."shipped_date" OR ("Target"."shipped_date" IS NULL AND "Source"."shipped_date" IS NULL))) THEN
  UPDATE SET
        "customer_id" = "Source"."customer_id",
        "employee_id" = "Source"."employee_id",
        "freight" = "Source"."freight",
        "order_date" = "Source"."order_date",
        "order_id" = "Source"."order_id",
        "required_date" = "Source"."required_date",
        "ship_address" = "Source"."ship_address",
        "ship_city" = "Source"."ship_city",
        "ship_country" = "Source"."ship_country",
        "ship_name" = "Source"."ship_name",
        "ship_postal_code" = "Source"."ship_postal_code",
        "ship_region" = "Source"."ship_region",
        "ship_via" = "Source"."ship_via",
        "shipped_date" = "Source"."shipped_date"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "customer_id",
        "employee_id",
        "freight",
        "order_date",
        "order_id",
        "required_date",
        "ship_address",
        "ship_city",
        "ship_country",
        "ship_name",
        "ship_postal_code",
        "ship_region",
        "ship_via",
        "shipped_date"
   ) 
  VALUES (
         "Source"."customer_id",
        "Source"."employee_id",
        "Source"."freight",
        "Source"."order_date",
        "Source"."order_id",
        "Source"."required_date",
        "Source"."ship_address",
        "Source"."ship_city",
        "Source"."ship_country",
        "Source"."ship_name",
        "Source"."ship_postal_code",
        "Source"."ship_region",
        "Source"."ship_via",
        "Source"."shipped_date"
   )
 ;



END $$ LANGUAGE plpgsql;
