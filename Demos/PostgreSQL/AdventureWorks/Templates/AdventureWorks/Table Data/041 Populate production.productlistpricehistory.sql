
DO $$
DECLARE
  v_json JSON = '{{production.productlistpricehistory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productlistpricehistory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'enddate')::timestamp(6) AS "enddate",
           (elem ->> 'listprice')::numeric AS "listprice",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'startdate')::timestamp(6) AS "startdate"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productid" = "Target"."productid" AND "Source"."startdate" = "Target"."startdate"

WHEN MATCHED AND (NOT ("Target"."enddate" = "Source"."enddate" OR ("Target"."enddate" IS NULL AND "Source"."enddate" IS NULL)) OR NOT ("Target"."listprice" = "Source"."listprice" OR ("Target"."listprice" IS NULL AND "Source"."listprice" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."startdate" = "Source"."startdate" OR ("Target"."startdate" IS NULL AND "Source"."startdate" IS NULL))) THEN
  UPDATE SET
        "enddate" = "Source"."enddate",
        "listprice" = "Source"."listprice",
        "modifieddate" = "Source"."modifieddate",
        "productid" = "Source"."productid",
        "startdate" = "Source"."startdate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "enddate",
        "listprice",
        "modifieddate",
        "productid",
        "startdate"
   ) 
  VALUES (
         "Source"."enddate",
        "Source"."listprice",
        "Source"."modifieddate",
        "Source"."productid",
        "Source"."startdate"
   )
 ;



END $$ LANGUAGE plpgsql;
