
DO $$
DECLARE
  v_json JSON = '{{sales.salesorderheadersalesreason.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salesorderheadersalesreason" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'salesorderid')::int4 AS "salesorderid",
           (elem ->> 'salesreasonid')::int4 AS "salesreasonid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."salesorderid" = "Target"."salesorderid" AND "Source"."salesreasonid" = "Target"."salesreasonid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."salesorderid" = "Source"."salesorderid" OR ("Target"."salesorderid" IS NULL AND "Source"."salesorderid" IS NULL)) OR NOT ("Target"."salesreasonid" = "Source"."salesreasonid" OR ("Target"."salesreasonid" IS NULL AND "Source"."salesreasonid" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "salesorderid" = "Source"."salesorderid",
        "salesreasonid" = "Source"."salesreasonid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "salesorderid",
        "salesreasonid"
   ) 
  VALUES (
         "Source"."modifieddate",
        "Source"."salesorderid",
        "Source"."salesreasonid"
   )
 ;



END $$ LANGUAGE plpgsql;
