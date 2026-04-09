
DO $$
DECLARE
  v_json JSON = '{{sales.shoppingcartitem.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."shoppingcartitem" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'datecreated')::timestamp(6) AS "datecreated",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'quantity')::int4 AS "quantity",
           (elem ->> 'shoppingcartid')::varchar(50) AS "shoppingcartid",
           (elem ->> 'shoppingcartitemid')::int4 AS "shoppingcartitemid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."shoppingcartitemid" = "Target"."shoppingcartitemid"

WHEN MATCHED AND (NOT ("Target"."datecreated" = "Source"."datecreated" OR ("Target"."datecreated" IS NULL AND "Source"."datecreated" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."quantity" = "Source"."quantity" OR ("Target"."quantity" IS NULL AND "Source"."quantity" IS NULL)) OR NOT ("Target"."shoppingcartid" = "Source"."shoppingcartid" OR ("Target"."shoppingcartid" IS NULL AND "Source"."shoppingcartid" IS NULL))) THEN
  UPDATE SET
        "datecreated" = "Source"."datecreated",
        "modifieddate" = "Source"."modifieddate",
        "productid" = "Source"."productid",
        "quantity" = "Source"."quantity",
        "shoppingcartid" = "Source"."shoppingcartid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "datecreated",
        "modifieddate",
        "productid",
        "quantity",
        "shoppingcartid",
        "shoppingcartitemid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."datecreated",
        "Source"."modifieddate",
        "Source"."productid",
        "Source"."quantity",
        "Source"."shoppingcartid",
        "Source"."shoppingcartitemid"
   )
 ;

SELECT SETVAL('sales.shoppingcartitem_shoppingcartitemid_seq', (SELECT MAX("shoppingcartitemid") FROM "sales"."shoppingcartitem")) INTO nextval;

END $$ LANGUAGE plpgsql;
