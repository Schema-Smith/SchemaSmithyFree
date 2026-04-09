
DO $$
DECLARE
  v_json JSON = '{{public.order_details.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."order_details" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'discount')::float4 AS "discount",
           (elem ->> 'order_id')::int2 AS "order_id",
           (elem ->> 'product_id')::int2 AS "product_id",
           (elem ->> 'quantity')::int2 AS "quantity",
           (elem ->> 'unit_price')::float4 AS "unit_price"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."order_id" = "Target"."order_id" AND "Source"."product_id" = "Target"."product_id"

WHEN MATCHED AND (NOT ("Target"."discount" = "Source"."discount" OR ("Target"."discount" IS NULL AND "Source"."discount" IS NULL)) OR NOT ("Target"."order_id" = "Source"."order_id" OR ("Target"."order_id" IS NULL AND "Source"."order_id" IS NULL)) OR NOT ("Target"."product_id" = "Source"."product_id" OR ("Target"."product_id" IS NULL AND "Source"."product_id" IS NULL)) OR NOT ("Target"."quantity" = "Source"."quantity" OR ("Target"."quantity" IS NULL AND "Source"."quantity" IS NULL)) OR NOT ("Target"."unit_price" = "Source"."unit_price" OR ("Target"."unit_price" IS NULL AND "Source"."unit_price" IS NULL))) THEN
  UPDATE SET
        "discount" = "Source"."discount",
        "order_id" = "Source"."order_id",
        "product_id" = "Source"."product_id",
        "quantity" = "Source"."quantity",
        "unit_price" = "Source"."unit_price"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "discount",
        "order_id",
        "product_id",
        "quantity",
        "unit_price"
   ) 
  VALUES (
         "Source"."discount",
        "Source"."order_id",
        "Source"."product_id",
        "Source"."quantity",
        "Source"."unit_price"
   )
 ;



END $$ LANGUAGE plpgsql;
