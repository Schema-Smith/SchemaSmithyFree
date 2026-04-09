
DO $$
DECLARE
  v_json JSON = '{{person.countryregion.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "person"."countryregion" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'countryregioncode')::varchar(3) AS "countryregioncode",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."countryregioncode" = "Target"."countryregioncode"

WHEN MATCHED AND (NOT ("Target"."countryregioncode" = "Source"."countryregioncode" OR ("Target"."countryregioncode" IS NULL AND "Source"."countryregioncode" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "countryregioncode" = "Source"."countryregioncode",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "countryregioncode",
        "modifieddate",
        "name"
   ) 
  VALUES (
         "Source"."countryregioncode",
        "Source"."modifieddate",
        "Source"."name"
   )
 ;



END $$ LANGUAGE plpgsql;
