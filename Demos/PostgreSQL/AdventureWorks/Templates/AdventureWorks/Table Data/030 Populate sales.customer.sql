
DO $$
DECLARE
  v_json JSON = '{{sales.customer.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."customer" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'customerid')::int4 AS "customerid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'personid')::int4 AS "personid",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'storeid')::int4 AS "storeid",
           (elem ->> 'territoryid')::int4 AS "territoryid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."customerid" = "Target"."customerid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."personid" = "Source"."personid" OR ("Target"."personid" IS NULL AND "Source"."personid" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."storeid" = "Source"."storeid" OR ("Target"."storeid" IS NULL AND "Source"."storeid" IS NULL)) OR NOT ("Target"."territoryid" = "Source"."territoryid" OR ("Target"."territoryid" IS NULL AND "Source"."territoryid" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "personid" = "Source"."personid",
        "rowguid" = "Source"."rowguid",
        "storeid" = "Source"."storeid",
        "territoryid" = "Source"."territoryid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "customerid",
        "modifieddate",
        "personid",
        "rowguid",
        "storeid",
        "territoryid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."customerid",
        "Source"."modifieddate",
        "Source"."personid",
        "Source"."rowguid",
        "Source"."storeid",
        "Source"."territoryid"
   )
 ;

SELECT SETVAL('sales.customer_customerid_seq', (SELECT MAX("customerid") FROM "sales"."customer")) INTO nextval;

END $$ LANGUAGE plpgsql;
