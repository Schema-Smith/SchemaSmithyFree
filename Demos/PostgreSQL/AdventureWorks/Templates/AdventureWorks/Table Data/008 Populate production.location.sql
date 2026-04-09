
DO $$
DECLARE
  v_json JSON = '{{production.location.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."location" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'availability')::numeric(8, 2) AS "availability",
           (elem ->> 'costrate')::numeric AS "costrate",
           (elem ->> 'locationid')::int4 AS "locationid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."locationid" = "Target"."locationid"

WHEN MATCHED AND (NOT ("Target"."availability" = "Source"."availability" OR ("Target"."availability" IS NULL AND "Source"."availability" IS NULL)) OR NOT ("Target"."costrate" = "Source"."costrate" OR ("Target"."costrate" IS NULL AND "Source"."costrate" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "availability" = "Source"."availability",
        "costrate" = "Source"."costrate",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "availability",
        "costrate",
        "locationid",
        "modifieddate",
        "name"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."availability",
        "Source"."costrate",
        "Source"."locationid",
        "Source"."modifieddate",
        "Source"."name"
   )
 ;

SELECT SETVAL('production.location_locationid_seq', (SELECT MAX("locationid") FROM "production"."location")) INTO nextval;

END $$ LANGUAGE plpgsql;
