
DO $$
DECLARE
  v_json JSON = '{{sales.salesterritory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salesterritory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'costlastyear')::numeric AS "costlastyear",
           (elem ->> 'costytd')::numeric AS "costytd",
           (elem ->> 'countryregioncode')::varchar(3) AS "countryregioncode",
           (elem ->> 'group')::varchar(50) AS "group",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'saleslastyear')::numeric AS "saleslastyear",
           (elem ->> 'salesytd')::numeric AS "salesytd",
           (elem ->> 'territoryid')::int4 AS "territoryid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."territoryid" = "Target"."territoryid"

WHEN MATCHED AND (NOT ("Target"."costlastyear" = "Source"."costlastyear" OR ("Target"."costlastyear" IS NULL AND "Source"."costlastyear" IS NULL)) OR NOT ("Target"."costytd" = "Source"."costytd" OR ("Target"."costytd" IS NULL AND "Source"."costytd" IS NULL)) OR NOT ("Target"."countryregioncode" = "Source"."countryregioncode" OR ("Target"."countryregioncode" IS NULL AND "Source"."countryregioncode" IS NULL)) OR NOT ("Target"."group" = "Source"."group" OR ("Target"."group" IS NULL AND "Source"."group" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."saleslastyear" = "Source"."saleslastyear" OR ("Target"."saleslastyear" IS NULL AND "Source"."saleslastyear" IS NULL)) OR NOT ("Target"."salesytd" = "Source"."salesytd" OR ("Target"."salesytd" IS NULL AND "Source"."salesytd" IS NULL))) THEN
  UPDATE SET
        "costlastyear" = "Source"."costlastyear",
        "costytd" = "Source"."costytd",
        "countryregioncode" = "Source"."countryregioncode",
        "group" = "Source"."group",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "rowguid" = "Source"."rowguid",
        "saleslastyear" = "Source"."saleslastyear",
        "salesytd" = "Source"."salesytd"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "costlastyear",
        "costytd",
        "countryregioncode",
        "group",
        "modifieddate",
        "name",
        "rowguid",
        "saleslastyear",
        "salesytd",
        "territoryid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."costlastyear",
        "Source"."costytd",
        "Source"."countryregioncode",
        "Source"."group",
        "Source"."modifieddate",
        "Source"."name",
        "Source"."rowguid",
        "Source"."saleslastyear",
        "Source"."salesytd",
        "Source"."territoryid"
   )
 ;

SELECT SETVAL('sales.salesterritory_territoryid_seq', (SELECT MAX("territoryid") FROM "sales"."salesterritory")) INTO nextval;

END $$ LANGUAGE plpgsql;
