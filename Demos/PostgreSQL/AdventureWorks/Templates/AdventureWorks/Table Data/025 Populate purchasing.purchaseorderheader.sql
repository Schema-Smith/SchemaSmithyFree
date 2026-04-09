
DO $$
DECLARE
  v_json JSON = '{{purchasing.purchaseorderheader.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "purchasing"."purchaseorderheader" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'employeeid')::int4 AS "employeeid",
           (elem ->> 'freight')::numeric AS "freight",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'orderdate')::timestamp(6) AS "orderdate",
           (elem ->> 'purchaseorderid')::int4 AS "purchaseorderid",
           (elem ->> 'revisionnumber')::int2 AS "revisionnumber",
           (elem ->> 'shipdate')::timestamp(6) AS "shipdate",
           (elem ->> 'shipmethodid')::int4 AS "shipmethodid",
           (elem ->> 'status')::int2 AS "status",
           (elem ->> 'subtotal')::numeric AS "subtotal",
           (elem ->> 'taxamt')::numeric AS "taxamt",
           (elem ->> 'vendorid')::int4 AS "vendorid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."purchaseorderid" = "Target"."purchaseorderid"

WHEN MATCHED AND (NOT ("Target"."employeeid" = "Source"."employeeid" OR ("Target"."employeeid" IS NULL AND "Source"."employeeid" IS NULL)) OR NOT ("Target"."freight" = "Source"."freight" OR ("Target"."freight" IS NULL AND "Source"."freight" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."orderdate" = "Source"."orderdate" OR ("Target"."orderdate" IS NULL AND "Source"."orderdate" IS NULL)) OR NOT ("Target"."revisionnumber" = "Source"."revisionnumber" OR ("Target"."revisionnumber" IS NULL AND "Source"."revisionnumber" IS NULL)) OR NOT ("Target"."shipdate" = "Source"."shipdate" OR ("Target"."shipdate" IS NULL AND "Source"."shipdate" IS NULL)) OR NOT ("Target"."shipmethodid" = "Source"."shipmethodid" OR ("Target"."shipmethodid" IS NULL AND "Source"."shipmethodid" IS NULL)) OR NOT ("Target"."status" = "Source"."status" OR ("Target"."status" IS NULL AND "Source"."status" IS NULL)) OR NOT ("Target"."subtotal" = "Source"."subtotal" OR ("Target"."subtotal" IS NULL AND "Source"."subtotal" IS NULL)) OR NOT ("Target"."taxamt" = "Source"."taxamt" OR ("Target"."taxamt" IS NULL AND "Source"."taxamt" IS NULL)) OR NOT ("Target"."vendorid" = "Source"."vendorid" OR ("Target"."vendorid" IS NULL AND "Source"."vendorid" IS NULL))) THEN
  UPDATE SET
        "employeeid" = "Source"."employeeid",
        "freight" = "Source"."freight",
        "modifieddate" = "Source"."modifieddate",
        "orderdate" = "Source"."orderdate",
        "revisionnumber" = "Source"."revisionnumber",
        "shipdate" = "Source"."shipdate",
        "shipmethodid" = "Source"."shipmethodid",
        "status" = "Source"."status",
        "subtotal" = "Source"."subtotal",
        "taxamt" = "Source"."taxamt",
        "vendorid" = "Source"."vendorid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "employeeid",
        "freight",
        "modifieddate",
        "orderdate",
        "purchaseorderid",
        "revisionnumber",
        "shipdate",
        "shipmethodid",
        "status",
        "subtotal",
        "taxamt",
        "vendorid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."employeeid",
        "Source"."freight",
        "Source"."modifieddate",
        "Source"."orderdate",
        "Source"."purchaseorderid",
        "Source"."revisionnumber",
        "Source"."shipdate",
        "Source"."shipmethodid",
        "Source"."status",
        "Source"."subtotal",
        "Source"."taxamt",
        "Source"."vendorid"
   )
 ;

SELECT SETVAL('purchasing.purchaseorderheader_purchaseorderid_seq', (SELECT MAX("purchaseorderid") FROM "purchasing"."purchaseorderheader")) INTO nextval;

END $$ LANGUAGE plpgsql;
