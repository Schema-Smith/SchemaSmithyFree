
DO $$
DECLARE
  v_json JSON = '{{production.productmodelproductdescriptionculture.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productmodelproductdescriptionculture" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'cultureid')::bpchar(6) AS "cultureid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productdescriptionid')::int4 AS "productdescriptionid",
           (elem ->> 'productmodelid')::int4 AS "productmodelid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productmodelid" = "Target"."productmodelid" AND "Source"."productdescriptionid" = "Target"."productdescriptionid" AND "Source"."cultureid" = "Target"."cultureid"

WHEN MATCHED AND (NOT ("Target"."cultureid" = "Source"."cultureid" OR ("Target"."cultureid" IS NULL AND "Source"."cultureid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productdescriptionid" = "Source"."productdescriptionid" OR ("Target"."productdescriptionid" IS NULL AND "Source"."productdescriptionid" IS NULL)) OR NOT ("Target"."productmodelid" = "Source"."productmodelid" OR ("Target"."productmodelid" IS NULL AND "Source"."productmodelid" IS NULL))) THEN
  UPDATE SET
        "cultureid" = "Source"."cultureid",
        "modifieddate" = "Source"."modifieddate",
        "productdescriptionid" = "Source"."productdescriptionid",
        "productmodelid" = "Source"."productmodelid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "cultureid",
        "modifieddate",
        "productdescriptionid",
        "productmodelid"
   ) 
  VALUES (
         "Source"."cultureid",
        "Source"."modifieddate",
        "Source"."productdescriptionid",
        "Source"."productmodelid"
   )
 ;



END $$ LANGUAGE plpgsql;
