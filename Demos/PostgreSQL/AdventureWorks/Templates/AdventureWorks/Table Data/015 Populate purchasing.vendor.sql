
DO $$
DECLARE
  v_json JSON = '{{purchasing.vendor.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "purchasing"."vendor" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'accountnumber')::varchar(15) AS "accountnumber",
           (elem ->> 'activeflag')::bool AS "activeflag",
           (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'creditrating')::int2 AS "creditrating",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'preferredvendorstatus')::bool AS "preferredvendorstatus",
           (elem ->> 'purchasingwebserviceurl')::varchar(1024) AS "purchasingwebserviceurl"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid"

WHEN MATCHED AND (NOT ("Target"."accountnumber" = "Source"."accountnumber" OR ("Target"."accountnumber" IS NULL AND "Source"."accountnumber" IS NULL)) OR NOT ("Target"."activeflag" = "Source"."activeflag" OR ("Target"."activeflag" IS NULL AND "Source"."activeflag" IS NULL)) OR NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."creditrating" = "Source"."creditrating" OR ("Target"."creditrating" IS NULL AND "Source"."creditrating" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."preferredvendorstatus" = "Source"."preferredvendorstatus" OR ("Target"."preferredvendorstatus" IS NULL AND "Source"."preferredvendorstatus" IS NULL)) OR NOT ("Target"."purchasingwebserviceurl" = "Source"."purchasingwebserviceurl" OR ("Target"."purchasingwebserviceurl" IS NULL AND "Source"."purchasingwebserviceurl" IS NULL))) THEN
  UPDATE SET
        "accountnumber" = "Source"."accountnumber",
        "activeflag" = "Source"."activeflag",
        "businessentityid" = "Source"."businessentityid",
        "creditrating" = "Source"."creditrating",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "preferredvendorstatus" = "Source"."preferredvendorstatus",
        "purchasingwebserviceurl" = "Source"."purchasingwebserviceurl"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "accountnumber",
        "activeflag",
        "businessentityid",
        "creditrating",
        "modifieddate",
        "name",
        "preferredvendorstatus",
        "purchasingwebserviceurl"
   ) 
  VALUES (
         "Source"."accountnumber",
        "Source"."activeflag",
        "Source"."businessentityid",
        "Source"."creditrating",
        "Source"."modifieddate",
        "Source"."name",
        "Source"."preferredvendorstatus",
        "Source"."purchasingwebserviceurl"
   )
 ;



END $$ LANGUAGE plpgsql;
