
DO $$
DECLARE
  v_json JSON = '{{humanresources.employeedepartmenthistory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "humanresources"."employeedepartmenthistory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'departmentid')::int2 AS "departmentid",
           (elem ->> 'enddate')::date AS "enddate",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'shiftid')::int2 AS "shiftid",
           (elem ->> 'startdate')::date AS "startdate"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid" AND "Source"."startdate" = "Target"."startdate" AND "Source"."departmentid" = "Target"."departmentid" AND "Source"."shiftid" = "Target"."shiftid"

WHEN MATCHED AND (NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."departmentid" = "Source"."departmentid" OR ("Target"."departmentid" IS NULL AND "Source"."departmentid" IS NULL)) OR NOT ("Target"."enddate" = "Source"."enddate" OR ("Target"."enddate" IS NULL AND "Source"."enddate" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."shiftid" = "Source"."shiftid" OR ("Target"."shiftid" IS NULL AND "Source"."shiftid" IS NULL)) OR NOT ("Target"."startdate" = "Source"."startdate" OR ("Target"."startdate" IS NULL AND "Source"."startdate" IS NULL))) THEN
  UPDATE SET
        "businessentityid" = "Source"."businessentityid",
        "departmentid" = "Source"."departmentid",
        "enddate" = "Source"."enddate",
        "modifieddate" = "Source"."modifieddate",
        "shiftid" = "Source"."shiftid",
        "startdate" = "Source"."startdate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "businessentityid",
        "departmentid",
        "enddate",
        "modifieddate",
        "shiftid",
        "startdate"
   ) 
  VALUES (
         "Source"."businessentityid",
        "Source"."departmentid",
        "Source"."enddate",
        "Source"."modifieddate",
        "Source"."shiftid",
        "Source"."startdate"
   )
 ;



END $$ LANGUAGE plpgsql;
