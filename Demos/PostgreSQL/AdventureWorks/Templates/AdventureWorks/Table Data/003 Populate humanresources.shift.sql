
DO $$
DECLARE
  v_json JSON = '{{humanresources.shift.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "humanresources"."shift" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'endtime')::time(6) AS "endtime",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'shiftid')::int4 AS "shiftid",
           (elem ->> 'starttime')::time(6) AS "starttime"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."shiftid" = "Target"."shiftid"

WHEN MATCHED AND (NOT ("Target"."endtime" = "Source"."endtime" OR ("Target"."endtime" IS NULL AND "Source"."endtime" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."starttime" = "Source"."starttime" OR ("Target"."starttime" IS NULL AND "Source"."starttime" IS NULL))) THEN
  UPDATE SET
        "endtime" = "Source"."endtime",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "starttime" = "Source"."starttime"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "endtime",
        "modifieddate",
        "name",
        "shiftid",
        "starttime"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."endtime",
        "Source"."modifieddate",
        "Source"."name",
        "Source"."shiftid",
        "Source"."starttime"
   )
 ;

SELECT SETVAL('humanresources.shift_shiftid_seq', (SELECT MAX("shiftid") FROM "humanresources"."shift")) INTO nextval;

END $$ LANGUAGE plpgsql;
