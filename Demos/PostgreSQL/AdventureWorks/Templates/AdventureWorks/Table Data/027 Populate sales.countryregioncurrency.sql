
DO $$
DECLARE
  v_json JSON = '{{sales.countryregioncurrency.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."countryregioncurrency" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'countryregioncode')::varchar(3) AS "countryregioncode",
           (elem ->> 'currencycode')::bpchar(3) AS "currencycode",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."countryregioncode" = "Target"."countryregioncode" AND "Source"."currencycode" = "Target"."currencycode"

WHEN MATCHED AND (NOT ("Target"."countryregioncode" = "Source"."countryregioncode" OR ("Target"."countryregioncode" IS NULL AND "Source"."countryregioncode" IS NULL)) OR NOT ("Target"."currencycode" = "Source"."currencycode" OR ("Target"."currencycode" IS NULL AND "Source"."currencycode" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL))) THEN
  UPDATE SET
        "countryregioncode" = "Source"."countryregioncode",
        "currencycode" = "Source"."currencycode",
        "modifieddate" = "Source"."modifieddate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "countryregioncode",
        "currencycode",
        "modifieddate"
   ) 
  VALUES (
         "Source"."countryregioncode",
        "Source"."currencycode",
        "Source"."modifieddate"
   )
 ;



END $$ LANGUAGE plpgsql;
