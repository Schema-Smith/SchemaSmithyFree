
DO $$
DECLARE
  v_json JSON = '{{public.products.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."products" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'category_id')::int2 AS "category_id",
           (elem ->> 'discontinued')::int4 AS "discontinued",
           (elem ->> 'product_id')::int2 AS "product_id",
           (elem ->> 'product_name')::varchar(40) AS "product_name",
           (elem ->> 'quantity_per_unit')::varchar(20) AS "quantity_per_unit",
           (elem ->> 'reorder_level')::int2 AS "reorder_level",
           (elem ->> 'supplier_id')::int2 AS "supplier_id",
           (elem ->> 'unit_price')::float4 AS "unit_price",
           (elem ->> 'units_in_stock')::int2 AS "units_in_stock",
           (elem ->> 'units_on_order')::int2 AS "units_on_order"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."product_id" = "Target"."product_id"

WHEN MATCHED AND (NOT ("Target"."category_id" = "Source"."category_id" OR ("Target"."category_id" IS NULL AND "Source"."category_id" IS NULL)) OR NOT ("Target"."discontinued" = "Source"."discontinued" OR ("Target"."discontinued" IS NULL AND "Source"."discontinued" IS NULL)) OR NOT ("Target"."product_id" = "Source"."product_id" OR ("Target"."product_id" IS NULL AND "Source"."product_id" IS NULL)) OR NOT ("Target"."product_name" = "Source"."product_name" OR ("Target"."product_name" IS NULL AND "Source"."product_name" IS NULL)) OR NOT ("Target"."quantity_per_unit" = "Source"."quantity_per_unit" OR ("Target"."quantity_per_unit" IS NULL AND "Source"."quantity_per_unit" IS NULL)) OR NOT ("Target"."reorder_level" = "Source"."reorder_level" OR ("Target"."reorder_level" IS NULL AND "Source"."reorder_level" IS NULL)) OR NOT ("Target"."supplier_id" = "Source"."supplier_id" OR ("Target"."supplier_id" IS NULL AND "Source"."supplier_id" IS NULL)) OR NOT ("Target"."unit_price" = "Source"."unit_price" OR ("Target"."unit_price" IS NULL AND "Source"."unit_price" IS NULL)) OR NOT ("Target"."units_in_stock" = "Source"."units_in_stock" OR ("Target"."units_in_stock" IS NULL AND "Source"."units_in_stock" IS NULL)) OR NOT ("Target"."units_on_order" = "Source"."units_on_order" OR ("Target"."units_on_order" IS NULL AND "Source"."units_on_order" IS NULL))) THEN
  UPDATE SET
        "category_id" = "Source"."category_id",
        "discontinued" = "Source"."discontinued",
        "product_id" = "Source"."product_id",
        "product_name" = "Source"."product_name",
        "quantity_per_unit" = "Source"."quantity_per_unit",
        "reorder_level" = "Source"."reorder_level",
        "supplier_id" = "Source"."supplier_id",
        "unit_price" = "Source"."unit_price",
        "units_in_stock" = "Source"."units_in_stock",
        "units_on_order" = "Source"."units_on_order"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "category_id",
        "discontinued",
        "product_id",
        "product_name",
        "quantity_per_unit",
        "reorder_level",
        "supplier_id",
        "unit_price",
        "units_in_stock",
        "units_on_order"
   ) 
  VALUES (
         "Source"."category_id",
        "Source"."discontinued",
        "Source"."product_id",
        "Source"."product_name",
        "Source"."quantity_per_unit",
        "Source"."reorder_level",
        "Source"."supplier_id",
        "Source"."unit_price",
        "Source"."units_in_stock",
        "Source"."units_on_order"
   )
 ;



END $$ LANGUAGE plpgsql;
