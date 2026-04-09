
DO $$
DECLARE
  v_json JSON = '{{person.stateprovince.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "person"."stateprovince" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'countryregioncode')::varchar(3) AS "countryregioncode",
           (elem ->> 'isonlystateprovinceflag')::bool AS "isonlystateprovinceflag",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'stateprovincecode')::bpchar(3) AS "stateprovincecode",
           (elem ->> 'stateprovinceid')::int4 AS "stateprovinceid",
           (elem ->> 'territoryid')::int4 AS "territoryid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."stateprovinceid" = "Target"."stateprovinceid"

WHEN MATCHED AND (NOT ("Target"."countryregioncode" = "Source"."countryregioncode" OR ("Target"."countryregioncode" IS NULL AND "Source"."countryregioncode" IS NULL)) OR NOT ("Target"."isonlystateprovinceflag" = "Source"."isonlystateprovinceflag" OR ("Target"."isonlystateprovinceflag" IS NULL AND "Source"."isonlystateprovinceflag" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."stateprovincecode" = "Source"."stateprovincecode" OR ("Target"."stateprovincecode" IS NULL AND "Source"."stateprovincecode" IS NULL)) OR NOT ("Target"."territoryid" = "Source"."territoryid" OR ("Target"."territoryid" IS NULL AND "Source"."territoryid" IS NULL))) THEN
  UPDATE SET
        "countryregioncode" = "Source"."countryregioncode",
        "isonlystateprovinceflag" = "Source"."isonlystateprovinceflag",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "rowguid" = "Source"."rowguid",
        "stateprovincecode" = "Source"."stateprovincecode",
        "territoryid" = "Source"."territoryid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "countryregioncode",
        "isonlystateprovinceflag",
        "modifieddate",
        "name",
        "rowguid",
        "stateprovincecode",
        "stateprovinceid",
        "territoryid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."countryregioncode",
        "Source"."isonlystateprovinceflag",
        "Source"."modifieddate",
        "Source"."name",
        "Source"."rowguid",
        "Source"."stateprovincecode",
        "Source"."stateprovinceid",
        "Source"."territoryid"
   )
 ;

SELECT SETVAL('person.stateprovince_stateprovinceid_seq', (SELECT MAX("stateprovinceid") FROM "person"."stateprovince")) INTO nextval;

END $$ LANGUAGE plpgsql;
