
DO $$
DECLARE
  v_json JSON = '{{public.employee_territories.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."employee_territories" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'employee_id')::int2 AS "employee_id",
           (elem ->> 'territory_id')::varchar(20) AS "territory_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."employee_id" = "Target"."employee_id" AND "Source"."territory_id" = "Target"."territory_id"

WHEN MATCHED AND (NOT ("Target"."employee_id" = "Source"."employee_id" OR ("Target"."employee_id" IS NULL AND "Source"."employee_id" IS NULL)) OR NOT ("Target"."territory_id" = "Source"."territory_id" OR ("Target"."territory_id" IS NULL AND "Source"."territory_id" IS NULL))) THEN
  UPDATE SET
        "employee_id" = "Source"."employee_id",
        "territory_id" = "Source"."territory_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "employee_id",
        "territory_id"
   ) 
  VALUES (
         "Source"."employee_id",
        "Source"."territory_id"
   )
 ;



END $$ LANGUAGE plpgsql;
