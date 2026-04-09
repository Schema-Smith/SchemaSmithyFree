
DO $$
DECLARE
  v_json JSON = '{{sales.salesorderheader.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salesorderheader" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'accountnumber')::varchar(15) AS "accountnumber",
           (elem ->> 'billtoaddressid')::int4 AS "billtoaddressid",
           (elem ->> 'comment')::varchar(128) AS "comment",
           (elem ->> 'creditcardapprovalcode')::varchar(15) AS "creditcardapprovalcode",
           (elem ->> 'creditcardid')::int4 AS "creditcardid",
           (elem ->> 'currencyrateid')::int4 AS "currencyrateid",
           (elem ->> 'customerid')::int4 AS "customerid",
           (elem ->> 'duedate')::timestamp(6) AS "duedate",
           (elem ->> 'freight')::numeric AS "freight",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'onlineorderflag')::bool AS "onlineorderflag",
           (elem ->> 'orderdate')::timestamp(6) AS "orderdate",
           (elem ->> 'purchaseordernumber')::varchar(25) AS "purchaseordernumber",
           (elem ->> 'revisionnumber')::int2 AS "revisionnumber",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'salesorderid')::int4 AS "salesorderid",
           (elem ->> 'salespersonid')::int4 AS "salespersonid",
           (elem ->> 'shipdate')::timestamp(6) AS "shipdate",
           (elem ->> 'shipmethodid')::int4 AS "shipmethodid",
           (elem ->> 'shiptoaddressid')::int4 AS "shiptoaddressid",
           (elem ->> 'status')::int2 AS "status",
           (elem ->> 'subtotal')::numeric AS "subtotal",
           (elem ->> 'taxamt')::numeric AS "taxamt",
           (elem ->> 'territoryid')::int4 AS "territoryid",
           (elem ->> 'totaldue')::numeric AS "totaldue"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."salesorderid" = "Target"."salesorderid"

WHEN MATCHED AND (NOT ("Target"."accountnumber" = "Source"."accountnumber" OR ("Target"."accountnumber" IS NULL AND "Source"."accountnumber" IS NULL)) OR NOT ("Target"."billtoaddressid" = "Source"."billtoaddressid" OR ("Target"."billtoaddressid" IS NULL AND "Source"."billtoaddressid" IS NULL)) OR NOT ("Target"."comment" = "Source"."comment" OR ("Target"."comment" IS NULL AND "Source"."comment" IS NULL)) OR NOT ("Target"."creditcardapprovalcode" = "Source"."creditcardapprovalcode" OR ("Target"."creditcardapprovalcode" IS NULL AND "Source"."creditcardapprovalcode" IS NULL)) OR NOT ("Target"."creditcardid" = "Source"."creditcardid" OR ("Target"."creditcardid" IS NULL AND "Source"."creditcardid" IS NULL)) OR NOT ("Target"."currencyrateid" = "Source"."currencyrateid" OR ("Target"."currencyrateid" IS NULL AND "Source"."currencyrateid" IS NULL)) OR NOT ("Target"."customerid" = "Source"."customerid" OR ("Target"."customerid" IS NULL AND "Source"."customerid" IS NULL)) OR NOT ("Target"."duedate" = "Source"."duedate" OR ("Target"."duedate" IS NULL AND "Source"."duedate" IS NULL)) OR NOT ("Target"."freight" = "Source"."freight" OR ("Target"."freight" IS NULL AND "Source"."freight" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."onlineorderflag" = "Source"."onlineorderflag" OR ("Target"."onlineorderflag" IS NULL AND "Source"."onlineorderflag" IS NULL)) OR NOT ("Target"."orderdate" = "Source"."orderdate" OR ("Target"."orderdate" IS NULL AND "Source"."orderdate" IS NULL)) OR NOT ("Target"."purchaseordernumber" = "Source"."purchaseordernumber" OR ("Target"."purchaseordernumber" IS NULL AND "Source"."purchaseordernumber" IS NULL)) OR NOT ("Target"."revisionnumber" = "Source"."revisionnumber" OR ("Target"."revisionnumber" IS NULL AND "Source"."revisionnumber" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."salespersonid" = "Source"."salespersonid" OR ("Target"."salespersonid" IS NULL AND "Source"."salespersonid" IS NULL)) OR NOT ("Target"."shipdate" = "Source"."shipdate" OR ("Target"."shipdate" IS NULL AND "Source"."shipdate" IS NULL)) OR NOT ("Target"."shipmethodid" = "Source"."shipmethodid" OR ("Target"."shipmethodid" IS NULL AND "Source"."shipmethodid" IS NULL)) OR NOT ("Target"."shiptoaddressid" = "Source"."shiptoaddressid" OR ("Target"."shiptoaddressid" IS NULL AND "Source"."shiptoaddressid" IS NULL)) OR NOT ("Target"."status" = "Source"."status" OR ("Target"."status" IS NULL AND "Source"."status" IS NULL)) OR NOT ("Target"."subtotal" = "Source"."subtotal" OR ("Target"."subtotal" IS NULL AND "Source"."subtotal" IS NULL)) OR NOT ("Target"."taxamt" = "Source"."taxamt" OR ("Target"."taxamt" IS NULL AND "Source"."taxamt" IS NULL)) OR NOT ("Target"."territoryid" = "Source"."territoryid" OR ("Target"."territoryid" IS NULL AND "Source"."territoryid" IS NULL)) OR NOT ("Target"."totaldue" = "Source"."totaldue" OR ("Target"."totaldue" IS NULL AND "Source"."totaldue" IS NULL))) THEN
  UPDATE SET
        "accountnumber" = "Source"."accountnumber",
        "billtoaddressid" = "Source"."billtoaddressid",
        "comment" = "Source"."comment",
        "creditcardapprovalcode" = "Source"."creditcardapprovalcode",
        "creditcardid" = "Source"."creditcardid",
        "currencyrateid" = "Source"."currencyrateid",
        "customerid" = "Source"."customerid",
        "duedate" = "Source"."duedate",
        "freight" = "Source"."freight",
        "modifieddate" = "Source"."modifieddate",
        "onlineorderflag" = "Source"."onlineorderflag",
        "orderdate" = "Source"."orderdate",
        "purchaseordernumber" = "Source"."purchaseordernumber",
        "revisionnumber" = "Source"."revisionnumber",
        "rowguid" = "Source"."rowguid",
        "salespersonid" = "Source"."salespersonid",
        "shipdate" = "Source"."shipdate",
        "shipmethodid" = "Source"."shipmethodid",
        "shiptoaddressid" = "Source"."shiptoaddressid",
        "status" = "Source"."status",
        "subtotal" = "Source"."subtotal",
        "taxamt" = "Source"."taxamt",
        "territoryid" = "Source"."territoryid",
        "totaldue" = "Source"."totaldue"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "accountnumber",
        "billtoaddressid",
        "comment",
        "creditcardapprovalcode",
        "creditcardid",
        "currencyrateid",
        "customerid",
        "duedate",
        "freight",
        "modifieddate",
        "onlineorderflag",
        "orderdate",
        "purchaseordernumber",
        "revisionnumber",
        "rowguid",
        "salesorderid",
        "salespersonid",
        "shipdate",
        "shipmethodid",
        "shiptoaddressid",
        "status",
        "subtotal",
        "taxamt",
        "territoryid",
        "totaldue"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."accountnumber",
        "Source"."billtoaddressid",
        "Source"."comment",
        "Source"."creditcardapprovalcode",
        "Source"."creditcardid",
        "Source"."currencyrateid",
        "Source"."customerid",
        "Source"."duedate",
        "Source"."freight",
        "Source"."modifieddate",
        "Source"."onlineorderflag",
        "Source"."orderdate",
        "Source"."purchaseordernumber",
        "Source"."revisionnumber",
        "Source"."rowguid",
        "Source"."salesorderid",
        "Source"."salespersonid",
        "Source"."shipdate",
        "Source"."shipmethodid",
        "Source"."shiptoaddressid",
        "Source"."status",
        "Source"."subtotal",
        "Source"."taxamt",
        "Source"."territoryid",
        "Source"."totaldue"
   )
 ;

SELECT SETVAL('sales.salesorderheader_salesorderid_seq', (SELECT MAX("salesorderid") FROM "sales"."salesorderheader")) INTO nextval;

END $$ LANGUAGE plpgsql;
