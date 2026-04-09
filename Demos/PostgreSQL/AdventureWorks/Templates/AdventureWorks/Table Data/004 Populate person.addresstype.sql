
DO $$
DECLARE
  v_json JSON = '{{person.addresstype.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "person"."addresstype" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'addresstypeid')::int4 AS "addresstypeid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'rowguid')::uuid AS "rowguid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."addresstypeid" = "Target"."addresstypeid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "rowguid" = "Source"."rowguid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "addresstypeid",
        "modifieddate",
        "name",
        "rowguid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."addresstypeid",
        "Source"."modifieddate",
        "Source"."name",
        "Source"."rowguid"
   )
 ;

SELECT SETVAL('person.addresstype_addresstypeid_seq', (SELECT MAX("addresstypeid") FROM "person"."addresstype")) INTO nextval;

END $$ LANGUAGE plpgsql;
