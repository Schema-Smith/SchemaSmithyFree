
DO $$
DECLARE
  v_json JSON = '{{production.productinventory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productinventory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'bin')::int2 AS "bin",
           (elem ->> 'locationid')::int2 AS "locationid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'quantity')::int2 AS "quantity",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'shelf')::varchar(10) AS "shelf"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productid" = "Target"."productid" AND "Source"."locationid" = "Target"."locationid"

WHEN MATCHED AND (NOT ("Target"."bin" = "Source"."bin" OR ("Target"."bin" IS NULL AND "Source"."bin" IS NULL)) OR NOT ("Target"."locationid" = "Source"."locationid" OR ("Target"."locationid" IS NULL AND "Source"."locationid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."quantity" = "Source"."quantity" OR ("Target"."quantity" IS NULL AND "Source"."quantity" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."shelf" = "Source"."shelf" OR ("Target"."shelf" IS NULL AND "Source"."shelf" IS NULL))) THEN
  UPDATE SET
        "bin" = "Source"."bin",
        "locationid" = "Source"."locationid",
        "modifieddate" = "Source"."modifieddate",
        "productid" = "Source"."productid",
        "quantity" = "Source"."quantity",
        "rowguid" = "Source"."rowguid",
        "shelf" = "Source"."shelf"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "bin",
        "locationid",
        "modifieddate",
        "productid",
        "quantity",
        "rowguid",
        "shelf"
   ) 
  VALUES (
         "Source"."bin",
        "Source"."locationid",
        "Source"."modifieddate",
        "Source"."productid",
        "Source"."quantity",
        "Source"."rowguid",
        "Source"."shelf"
   )
 ;



END $$ LANGUAGE plpgsql;
