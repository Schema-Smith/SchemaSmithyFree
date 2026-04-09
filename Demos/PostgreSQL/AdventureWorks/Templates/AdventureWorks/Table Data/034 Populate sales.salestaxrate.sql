
DO $$
DECLARE
  v_json JSON = '{{sales.salestaxrate.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salestaxrate" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'salestaxrateid')::int4 AS "salestaxrateid",
           (elem ->> 'stateprovinceid')::int4 AS "stateprovinceid",
           (elem ->> 'taxrate')::numeric AS "taxrate",
           (elem ->> 'taxtype')::int2 AS "taxtype"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."salestaxrateid" = "Target"."salestaxrateid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."stateprovinceid" = "Source"."stateprovinceid" OR ("Target"."stateprovinceid" IS NULL AND "Source"."stateprovinceid" IS NULL)) OR NOT ("Target"."taxrate" = "Source"."taxrate" OR ("Target"."taxrate" IS NULL AND "Source"."taxrate" IS NULL)) OR NOT ("Target"."taxtype" = "Source"."taxtype" OR ("Target"."taxtype" IS NULL AND "Source"."taxtype" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "rowguid" = "Source"."rowguid",
        "stateprovinceid" = "Source"."stateprovinceid",
        "taxrate" = "Source"."taxrate",
        "taxtype" = "Source"."taxtype"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "name",
        "rowguid",
        "salestaxrateid",
        "stateprovinceid",
        "taxrate",
        "taxtype"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."modifieddate",
        "Source"."name",
        "Source"."rowguid",
        "Source"."salestaxrateid",
        "Source"."stateprovinceid",
        "Source"."taxrate",
        "Source"."taxtype"
   )
 ;

SELECT SETVAL('sales.salestaxrate_salestaxrateid_seq', (SELECT MAX("salestaxrateid") FROM "sales"."salestaxrate")) INTO nextval;

END $$ LANGUAGE plpgsql;
