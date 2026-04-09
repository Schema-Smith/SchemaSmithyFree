
DO $$
DECLARE
  v_json JSON = '{{production.unitmeasure.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."unitmeasure" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'unitmeasurecode')::bpchar(3) AS "unitmeasurecode"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."unitmeasurecode" = "Target"."unitmeasurecode"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."unitmeasurecode" = "Source"."unitmeasurecode" OR ("Target"."unitmeasurecode" IS NULL AND "Source"."unitmeasurecode" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "unitmeasurecode" = "Source"."unitmeasurecode"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "name",
        "unitmeasurecode"
   ) 
  VALUES (
         "Source"."modifieddate",
        "Source"."name",
        "Source"."unitmeasurecode"
   )
 ;



END $$ LANGUAGE plpgsql;
