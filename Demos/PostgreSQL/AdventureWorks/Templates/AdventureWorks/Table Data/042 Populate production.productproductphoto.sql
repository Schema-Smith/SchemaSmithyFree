
DO $$
DECLARE
  v_json JSON = '{{production.productproductphoto.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productproductphoto" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'primary')::bool AS "primary",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'productphotoid')::int4 AS "productphotoid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productid" = "Target"."productid" AND "Source"."productphotoid" = "Target"."productphotoid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."primary" = "Source"."primary" OR ("Target"."primary" IS NULL AND "Source"."primary" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."productphotoid" = "Source"."productphotoid" OR ("Target"."productphotoid" IS NULL AND "Source"."productphotoid" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "primary" = "Source"."primary",
        "productid" = "Source"."productid",
        "productphotoid" = "Source"."productphotoid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "primary",
        "productid",
        "productphotoid"
   ) 
  VALUES (
         "Source"."modifieddate",
        "Source"."primary",
        "Source"."productid",
        "Source"."productphotoid"
   )
 ;



END $$ LANGUAGE plpgsql;
