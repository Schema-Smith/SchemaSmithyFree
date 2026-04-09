
DO $$
DECLARE
  v_json JSON = '{{sales.currency.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."currency" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'currencycode')::bpchar(3) AS "currencycode",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."currencycode" = "Target"."currencycode"

WHEN MATCHED AND (NOT ("Target"."currencycode" = "Source"."currencycode" OR ("Target"."currencycode" IS NULL AND "Source"."currencycode" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "currencycode" = "Source"."currencycode",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "currencycode",
        "modifieddate",
        "name"
   ) 
  VALUES (
         "Source"."currencycode",
        "Source"."modifieddate",
        "Source"."name"
   )
 ;



END $$ LANGUAGE plpgsql;
