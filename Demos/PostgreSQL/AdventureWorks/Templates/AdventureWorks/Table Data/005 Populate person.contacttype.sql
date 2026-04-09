
DO $$
DECLARE
  v_json JSON = '{{person.contacttype.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "person"."contacttype" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'contacttypeid')::int4 AS "contacttypeid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."contacttypeid" = "Target"."contacttypeid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "contacttypeid",
        "modifieddate",
        "name"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."contacttypeid",
        "Source"."modifieddate",
        "Source"."name"
   )
 ;

SELECT SETVAL('person.contacttype_contacttypeid_seq', (SELECT MAX("contacttypeid") FROM "person"."contacttype")) INTO nextval;

END $$ LANGUAGE plpgsql;
