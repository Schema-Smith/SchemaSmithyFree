
DO $$
DECLARE
  v_json JSON = '{{sales.specialofferproduct.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."specialofferproduct" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'specialofferid')::int4 AS "specialofferid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."specialofferid" = "Target"."specialofferid" AND "Source"."productid" = "Target"."productid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."specialofferid" = "Source"."specialofferid" OR ("Target"."specialofferid" IS NULL AND "Source"."specialofferid" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "productid" = "Source"."productid",
        "rowguid" = "Source"."rowguid",
        "specialofferid" = "Source"."specialofferid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "productid",
        "rowguid",
        "specialofferid"
   ) 
  VALUES (
         "Source"."modifieddate",
        "Source"."productid",
        "Source"."rowguid",
        "Source"."specialofferid"
   )
 ;



END $$ LANGUAGE plpgsql;
