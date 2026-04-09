
DO $$
DECLARE
  v_json JSON = '{{sales.personcreditcard.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."personcreditcard" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'creditcardid')::int4 AS "creditcardid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid" AND "Source"."creditcardid" = "Target"."creditcardid"

WHEN MATCHED AND (NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."creditcardid" = "Source"."creditcardid" OR ("Target"."creditcardid" IS NULL AND "Source"."creditcardid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL))) THEN
  UPDATE SET
        "businessentityid" = "Source"."businessentityid",
        "creditcardid" = "Source"."creditcardid",
        "modifieddate" = "Source"."modifieddate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "businessentityid",
        "creditcardid",
        "modifieddate"
   ) 
  VALUES (
         "Source"."businessentityid",
        "Source"."creditcardid",
        "Source"."modifieddate"
   )
 ;



END $$ LANGUAGE plpgsql;
