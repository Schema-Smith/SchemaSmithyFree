
DO $$
DECLARE
  v_json JSON = '{{production.culture.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."culture" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'cultureid')::bpchar(6) AS "cultureid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."cultureid" = "Target"."cultureid"

WHEN MATCHED AND (NOT ("Target"."cultureid" = "Source"."cultureid" OR ("Target"."cultureid" IS NULL AND "Source"."cultureid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "cultureid" = "Source"."cultureid",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "cultureid",
        "modifieddate",
        "name"
   ) 
  VALUES (
         "Source"."cultureid",
        "Source"."modifieddate",
        "Source"."name"
   )
 ;



END $$ LANGUAGE plpgsql;
