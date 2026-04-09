
DO $$
DECLARE
  v_json JSON = '{{production.billofmaterials.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."billofmaterials" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'billofmaterialsid')::int4 AS "billofmaterialsid",
           (elem ->> 'bomlevel')::int2 AS "bomlevel",
           (elem ->> 'componentid')::int4 AS "componentid",
           (elem ->> 'enddate')::timestamp(6) AS "enddate",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'perassemblyqty')::numeric(8, 2) AS "perassemblyqty",
           (elem ->> 'productassemblyid')::int4 AS "productassemblyid",
           (elem ->> 'startdate')::timestamp(6) AS "startdate",
           (elem ->> 'unitmeasurecode')::bpchar(3) AS "unitmeasurecode"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."billofmaterialsid" = "Target"."billofmaterialsid"

WHEN MATCHED AND (NOT ("Target"."bomlevel" = "Source"."bomlevel" OR ("Target"."bomlevel" IS NULL AND "Source"."bomlevel" IS NULL)) OR NOT ("Target"."componentid" = "Source"."componentid" OR ("Target"."componentid" IS NULL AND "Source"."componentid" IS NULL)) OR NOT ("Target"."enddate" = "Source"."enddate" OR ("Target"."enddate" IS NULL AND "Source"."enddate" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."perassemblyqty" = "Source"."perassemblyqty" OR ("Target"."perassemblyqty" IS NULL AND "Source"."perassemblyqty" IS NULL)) OR NOT ("Target"."productassemblyid" = "Source"."productassemblyid" OR ("Target"."productassemblyid" IS NULL AND "Source"."productassemblyid" IS NULL)) OR NOT ("Target"."startdate" = "Source"."startdate" OR ("Target"."startdate" IS NULL AND "Source"."startdate" IS NULL)) OR NOT ("Target"."unitmeasurecode" = "Source"."unitmeasurecode" OR ("Target"."unitmeasurecode" IS NULL AND "Source"."unitmeasurecode" IS NULL))) THEN
  UPDATE SET
        "bomlevel" = "Source"."bomlevel",
        "componentid" = "Source"."componentid",
        "enddate" = "Source"."enddate",
        "modifieddate" = "Source"."modifieddate",
        "perassemblyqty" = "Source"."perassemblyqty",
        "productassemblyid" = "Source"."productassemblyid",
        "startdate" = "Source"."startdate",
        "unitmeasurecode" = "Source"."unitmeasurecode"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "billofmaterialsid",
        "bomlevel",
        "componentid",
        "enddate",
        "modifieddate",
        "perassemblyqty",
        "productassemblyid",
        "startdate",
        "unitmeasurecode"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."billofmaterialsid",
        "Source"."bomlevel",
        "Source"."componentid",
        "Source"."enddate",
        "Source"."modifieddate",
        "Source"."perassemblyqty",
        "Source"."productassemblyid",
        "Source"."startdate",
        "Source"."unitmeasurecode"
   )
 ;

SELECT SETVAL('production.billofmaterials_billofmaterialsid_seq', (SELECT MAX("billofmaterialsid") FROM "production"."billofmaterials")) INTO nextval;

END $$ LANGUAGE plpgsql;
