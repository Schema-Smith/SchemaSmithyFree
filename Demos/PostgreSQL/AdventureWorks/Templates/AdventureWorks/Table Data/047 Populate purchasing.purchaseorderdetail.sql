
DO $$
DECLARE
  v_json JSON = '{{purchasing.purchaseorderdetail.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "purchasing"."purchaseorderdetail" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'duedate')::timestamp(6) AS "duedate",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'orderqty')::int2 AS "orderqty",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'purchaseorderdetailid')::int4 AS "purchaseorderdetailid",
           (elem ->> 'purchaseorderid')::int4 AS "purchaseorderid",
           (elem ->> 'receivedqty')::numeric(8, 2) AS "receivedqty",
           (elem ->> 'rejectedqty')::numeric(8, 2) AS "rejectedqty",
           (elem ->> 'unitprice')::numeric AS "unitprice"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."purchaseorderid" = "Target"."purchaseorderid" AND "Source"."purchaseorderdetailid" = "Target"."purchaseorderdetailid"

WHEN MATCHED AND (NOT ("Target"."duedate" = "Source"."duedate" OR ("Target"."duedate" IS NULL AND "Source"."duedate" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."orderqty" = "Source"."orderqty" OR ("Target"."orderqty" IS NULL AND "Source"."orderqty" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."purchaseorderid" = "Source"."purchaseorderid" OR ("Target"."purchaseorderid" IS NULL AND "Source"."purchaseorderid" IS NULL)) OR NOT ("Target"."receivedqty" = "Source"."receivedqty" OR ("Target"."receivedqty" IS NULL AND "Source"."receivedqty" IS NULL)) OR NOT ("Target"."rejectedqty" = "Source"."rejectedqty" OR ("Target"."rejectedqty" IS NULL AND "Source"."rejectedqty" IS NULL)) OR NOT ("Target"."unitprice" = "Source"."unitprice" OR ("Target"."unitprice" IS NULL AND "Source"."unitprice" IS NULL))) THEN
  UPDATE SET
        "duedate" = "Source"."duedate",
        "modifieddate" = "Source"."modifieddate",
        "orderqty" = "Source"."orderqty",
        "productid" = "Source"."productid",
        "purchaseorderid" = "Source"."purchaseorderid",
        "receivedqty" = "Source"."receivedqty",
        "rejectedqty" = "Source"."rejectedqty",
        "unitprice" = "Source"."unitprice"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "duedate",
        "modifieddate",
        "orderqty",
        "productid",
        "purchaseorderdetailid",
        "purchaseorderid",
        "receivedqty",
        "rejectedqty",
        "unitprice"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."duedate",
        "Source"."modifieddate",
        "Source"."orderqty",
        "Source"."productid",
        "Source"."purchaseorderdetailid",
        "Source"."purchaseorderid",
        "Source"."receivedqty",
        "Source"."rejectedqty",
        "Source"."unitprice"
   )
 ;

SELECT SETVAL('purchasing.purchaseorderdetail_purchaseorderdetailid_seq', (SELECT MAX("purchaseorderdetailid") FROM "purchasing"."purchaseorderdetail")) INTO nextval;

END $$ LANGUAGE plpgsql;
