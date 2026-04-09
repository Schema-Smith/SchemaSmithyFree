
DO $$
DECLARE
  v_json JSON = '{{production.transactionhistory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."transactionhistory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'actualcost')::numeric AS "actualcost",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'quantity')::int4 AS "quantity",
           (elem ->> 'referenceorderid')::int4 AS "referenceorderid",
           (elem ->> 'referenceorderlineid')::int4 AS "referenceorderlineid",
           (elem ->> 'transactiondate')::timestamp(6) AS "transactiondate",
           (elem ->> 'transactionid')::int4 AS "transactionid",
           (elem ->> 'transactiontype')::bpchar(1) AS "transactiontype"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."transactionid" = "Target"."transactionid"

WHEN MATCHED AND (NOT ("Target"."actualcost" = "Source"."actualcost" OR ("Target"."actualcost" IS NULL AND "Source"."actualcost" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."quantity" = "Source"."quantity" OR ("Target"."quantity" IS NULL AND "Source"."quantity" IS NULL)) OR NOT ("Target"."referenceorderid" = "Source"."referenceorderid" OR ("Target"."referenceorderid" IS NULL AND "Source"."referenceorderid" IS NULL)) OR NOT ("Target"."referenceorderlineid" = "Source"."referenceorderlineid" OR ("Target"."referenceorderlineid" IS NULL AND "Source"."referenceorderlineid" IS NULL)) OR NOT ("Target"."transactiondate" = "Source"."transactiondate" OR ("Target"."transactiondate" IS NULL AND "Source"."transactiondate" IS NULL)) OR NOT ("Target"."transactiontype" = "Source"."transactiontype" OR ("Target"."transactiontype" IS NULL AND "Source"."transactiontype" IS NULL))) THEN
  UPDATE SET
        "actualcost" = "Source"."actualcost",
        "modifieddate" = "Source"."modifieddate",
        "productid" = "Source"."productid",
        "quantity" = "Source"."quantity",
        "referenceorderid" = "Source"."referenceorderid",
        "referenceorderlineid" = "Source"."referenceorderlineid",
        "transactiondate" = "Source"."transactiondate",
        "transactiontype" = "Source"."transactiontype"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "actualcost",
        "modifieddate",
        "productid",
        "quantity",
        "referenceorderid",
        "referenceorderlineid",
        "transactiondate",
        "transactionid",
        "transactiontype"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."actualcost",
        "Source"."modifieddate",
        "Source"."productid",
        "Source"."quantity",
        "Source"."referenceorderid",
        "Source"."referenceorderlineid",
        "Source"."transactiondate",
        "Source"."transactionid",
        "Source"."transactiontype"
   )
 ;

SELECT SETVAL('production.transactionhistory_transactionid_seq', (SELECT MAX("transactionid") FROM "production"."transactionhistory")) INTO nextval;

END $$ LANGUAGE plpgsql;
