
DO $$
DECLARE
  v_json JSON = '{{production.productmodelillustration.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productmodelillustration" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'illustrationid')::int4 AS "illustrationid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productmodelid')::int4 AS "productmodelid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productmodelid" = "Target"."productmodelid" AND "Source"."illustrationid" = "Target"."illustrationid"

WHEN MATCHED AND (NOT ("Target"."illustrationid" = "Source"."illustrationid" OR ("Target"."illustrationid" IS NULL AND "Source"."illustrationid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productmodelid" = "Source"."productmodelid" OR ("Target"."productmodelid" IS NULL AND "Source"."productmodelid" IS NULL))) THEN
  UPDATE SET
        "illustrationid" = "Source"."illustrationid",
        "modifieddate" = "Source"."modifieddate",
        "productmodelid" = "Source"."productmodelid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "illustrationid",
        "modifieddate",
        "productmodelid"
   ) 
  VALUES (
         "Source"."illustrationid",
        "Source"."modifieddate",
        "Source"."productmodelid"
   )
 ;



END $$ LANGUAGE plpgsql;
