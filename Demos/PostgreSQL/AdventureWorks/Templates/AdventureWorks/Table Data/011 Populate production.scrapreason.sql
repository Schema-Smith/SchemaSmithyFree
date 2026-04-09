
DO $$
DECLARE
  v_json JSON = '{{production.scrapreason.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."scrapreason" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'scrapreasonid')::int4 AS "scrapreasonid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."scrapreasonid" = "Target"."scrapreasonid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "name",
        "scrapreasonid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."modifieddate",
        "Source"."name",
        "Source"."scrapreasonid"
   )
 ;

SELECT SETVAL('production.scrapreason_scrapreasonid_seq', (SELECT MAX("scrapreasonid") FROM "production"."scrapreason")) INTO nextval;

END $$ LANGUAGE plpgsql;
