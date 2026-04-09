
DO $$
DECLARE
  v_json JSON = '{{humanresources.employeepayhistory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "humanresources"."employeepayhistory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'payfrequency')::int2 AS "payfrequency",
           (elem ->> 'rate')::numeric AS "rate",
           (elem ->> 'ratechangedate')::timestamp(6) AS "ratechangedate"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid" AND "Source"."ratechangedate" = "Target"."ratechangedate"

WHEN MATCHED AND (NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."payfrequency" = "Source"."payfrequency" OR ("Target"."payfrequency" IS NULL AND "Source"."payfrequency" IS NULL)) OR NOT ("Target"."rate" = "Source"."rate" OR ("Target"."rate" IS NULL AND "Source"."rate" IS NULL)) OR NOT ("Target"."ratechangedate" = "Source"."ratechangedate" OR ("Target"."ratechangedate" IS NULL AND "Source"."ratechangedate" IS NULL))) THEN
  UPDATE SET
        "businessentityid" = "Source"."businessentityid",
        "modifieddate" = "Source"."modifieddate",
        "payfrequency" = "Source"."payfrequency",
        "rate" = "Source"."rate",
        "ratechangedate" = "Source"."ratechangedate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "businessentityid",
        "modifieddate",
        "payfrequency",
        "rate",
        "ratechangedate"
   ) 
  VALUES (
         "Source"."businessentityid",
        "Source"."modifieddate",
        "Source"."payfrequency",
        "Source"."rate",
        "Source"."ratechangedate"
   )
 ;



END $$ LANGUAGE plpgsql;
