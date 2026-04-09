
DO $$
DECLARE
  v_json JSON = '{{production.productdocument.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productdocument" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'documentnode')::varchar AS "documentnode",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productid')::int4 AS "productid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productid" = "Target"."productid" AND "Source"."documentnode" = "Target"."documentnode"

WHEN MATCHED AND (NOT ("Target"."documentnode" = "Source"."documentnode" OR ("Target"."documentnode" IS NULL AND "Source"."documentnode" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL))) THEN
  UPDATE SET
        "documentnode" = "Source"."documentnode",
        "modifieddate" = "Source"."modifieddate",
        "productid" = "Source"."productid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "documentnode",
        "modifieddate",
        "productid"
   ) 
  VALUES (
         "Source"."documentnode",
        "Source"."modifieddate",
        "Source"."productid"
   )
 ;



END $$ LANGUAGE plpgsql;
