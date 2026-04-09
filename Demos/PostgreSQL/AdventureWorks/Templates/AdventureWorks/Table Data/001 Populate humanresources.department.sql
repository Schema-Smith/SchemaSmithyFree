
DO $$
DECLARE
  v_json JSON = '{{humanresources.department.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "humanresources"."department" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'departmentid')::int4 AS "departmentid",
           (elem ->> 'groupname')::varchar(50) AS "groupname",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."departmentid" = "Target"."departmentid"

WHEN MATCHED AND (NOT ("Target"."groupname" = "Source"."groupname" OR ("Target"."groupname" IS NULL AND "Source"."groupname" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "groupname" = "Source"."groupname",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "departmentid",
        "groupname",
        "modifieddate",
        "name"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."departmentid",
        "Source"."groupname",
        "Source"."modifieddate",
        "Source"."name"
   )
 ;

SELECT SETVAL('humanresources.department_departmentid_seq', (SELECT MAX("departmentid") FROM "humanresources"."department")) INTO nextval;

END $$ LANGUAGE plpgsql;
