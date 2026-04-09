
DO $$
DECLARE
  v_json JSON = '{{sales.salesorderdetail.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salesorderdetail" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'carriertrackingnumber')::varchar(25) AS "carriertrackingnumber",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'orderqty')::int2 AS "orderqty",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'salesorderdetailid')::int4 AS "salesorderdetailid",
           (elem ->> 'salesorderid')::int4 AS "salesorderid",
           (elem ->> 'specialofferid')::int4 AS "specialofferid",
           (elem ->> 'unitprice')::numeric AS "unitprice",
           (elem ->> 'unitpricediscount')::numeric AS "unitpricediscount"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."salesorderid" = "Target"."salesorderid" AND "Source"."salesorderdetailid" = "Target"."salesorderdetailid"

WHEN MATCHED AND (NOT ("Target"."carriertrackingnumber" = "Source"."carriertrackingnumber" OR ("Target"."carriertrackingnumber" IS NULL AND "Source"."carriertrackingnumber" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."orderqty" = "Source"."orderqty" OR ("Target"."orderqty" IS NULL AND "Source"."orderqty" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."salesorderid" = "Source"."salesorderid" OR ("Target"."salesorderid" IS NULL AND "Source"."salesorderid" IS NULL)) OR NOT ("Target"."specialofferid" = "Source"."specialofferid" OR ("Target"."specialofferid" IS NULL AND "Source"."specialofferid" IS NULL)) OR NOT ("Target"."unitprice" = "Source"."unitprice" OR ("Target"."unitprice" IS NULL AND "Source"."unitprice" IS NULL)) OR NOT ("Target"."unitpricediscount" = "Source"."unitpricediscount" OR ("Target"."unitpricediscount" IS NULL AND "Source"."unitpricediscount" IS NULL))) THEN
  UPDATE SET
        "carriertrackingnumber" = "Source"."carriertrackingnumber",
        "modifieddate" = "Source"."modifieddate",
        "orderqty" = "Source"."orderqty",
        "productid" = "Source"."productid",
        "rowguid" = "Source"."rowguid",
        "salesorderid" = "Source"."salesorderid",
        "specialofferid" = "Source"."specialofferid",
        "unitprice" = "Source"."unitprice",
        "unitpricediscount" = "Source"."unitpricediscount"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "carriertrackingnumber",
        "modifieddate",
        "orderqty",
        "productid",
        "rowguid",
        "salesorderdetailid",
        "salesorderid",
        "specialofferid",
        "unitprice",
        "unitpricediscount"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."carriertrackingnumber",
        "Source"."modifieddate",
        "Source"."orderqty",
        "Source"."productid",
        "Source"."rowguid",
        "Source"."salesorderdetailid",
        "Source"."salesorderid",
        "Source"."specialofferid",
        "Source"."unitprice",
        "Source"."unitpricediscount"
   )
 ;

SELECT SETVAL('sales.salesorderdetail_salesorderdetailid_seq', (SELECT MAX("salesorderdetailid") FROM "sales"."salesorderdetail")) INTO nextval;

END $$ LANGUAGE plpgsql;
