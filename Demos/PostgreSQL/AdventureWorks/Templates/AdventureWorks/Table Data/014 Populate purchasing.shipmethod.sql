
DO $$
DECLARE
  v_json JSON = '{{purchasing.shipmethod.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "purchasing"."shipmethod" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'shipbase')::numeric AS "shipbase",
           (elem ->> 'shipmethodid')::int4 AS "shipmethodid",
           (elem ->> 'shiprate')::numeric AS "shiprate"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."shipmethodid" = "Target"."shipmethodid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."shipbase" = "Source"."shipbase" OR ("Target"."shipbase" IS NULL AND "Source"."shipbase" IS NULL)) OR NOT ("Target"."shiprate" = "Source"."shiprate" OR ("Target"."shiprate" IS NULL AND "Source"."shiprate" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "rowguid" = "Source"."rowguid",
        "shipbase" = "Source"."shipbase",
        "shiprate" = "Source"."shiprate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "name",
        "rowguid",
        "shipbase",
        "shipmethodid",
        "shiprate"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."modifieddate",
        "Source"."name",
        "Source"."rowguid",
        "Source"."shipbase",
        "Source"."shipmethodid",
        "Source"."shiprate"
   )
 ;

SELECT SETVAL('purchasing.shipmethod_shipmethodid_seq', (SELECT MAX("shipmethodid") FROM "purchasing"."shipmethod")) INTO nextval;

END $$ LANGUAGE plpgsql;
