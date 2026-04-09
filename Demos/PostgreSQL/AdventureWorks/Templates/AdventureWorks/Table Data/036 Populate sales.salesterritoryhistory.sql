
DO $$
DECLARE
  v_json JSON = '{{sales.salesterritoryhistory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salesterritoryhistory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'enddate')::timestamp(6) AS "enddate",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'startdate')::timestamp(6) AS "startdate",
           (elem ->> 'territoryid')::int4 AS "territoryid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid" AND "Source"."startdate" = "Target"."startdate" AND "Source"."territoryid" = "Target"."territoryid"

WHEN MATCHED AND (NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."enddate" = "Source"."enddate" OR ("Target"."enddate" IS NULL AND "Source"."enddate" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."startdate" = "Source"."startdate" OR ("Target"."startdate" IS NULL AND "Source"."startdate" IS NULL)) OR NOT ("Target"."territoryid" = "Source"."territoryid" OR ("Target"."territoryid" IS NULL AND "Source"."territoryid" IS NULL))) THEN
  UPDATE SET
        "businessentityid" = "Source"."businessentityid",
        "enddate" = "Source"."enddate",
        "modifieddate" = "Source"."modifieddate",
        "rowguid" = "Source"."rowguid",
        "startdate" = "Source"."startdate",
        "territoryid" = "Source"."territoryid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "businessentityid",
        "enddate",
        "modifieddate",
        "rowguid",
        "startdate",
        "territoryid"
   ) 
  VALUES (
         "Source"."businessentityid",
        "Source"."enddate",
        "Source"."modifieddate",
        "Source"."rowguid",
        "Source"."startdate",
        "Source"."territoryid"
   )
 ;



END $$ LANGUAGE plpgsql;
