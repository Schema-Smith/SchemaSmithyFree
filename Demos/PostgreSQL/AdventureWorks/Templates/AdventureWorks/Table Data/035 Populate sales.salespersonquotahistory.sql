
DO $$
DECLARE
  v_json JSON = '{{sales.salespersonquotahistory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salespersonquotahistory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'quotadate')::timestamp(6) AS "quotadate",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'salesquota')::numeric AS "salesquota"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid" AND "Source"."quotadate" = "Target"."quotadate"

WHEN MATCHED AND (NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."quotadate" = "Source"."quotadate" OR ("Target"."quotadate" IS NULL AND "Source"."quotadate" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."salesquota" = "Source"."salesquota" OR ("Target"."salesquota" IS NULL AND "Source"."salesquota" IS NULL))) THEN
  UPDATE SET
        "businessentityid" = "Source"."businessentityid",
        "modifieddate" = "Source"."modifieddate",
        "quotadate" = "Source"."quotadate",
        "rowguid" = "Source"."rowguid",
        "salesquota" = "Source"."salesquota"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "businessentityid",
        "modifieddate",
        "quotadate",
        "rowguid",
        "salesquota"
   ) 
  VALUES (
         "Source"."businessentityid",
        "Source"."modifieddate",
        "Source"."quotadate",
        "Source"."rowguid",
        "Source"."salesquota"
   )
 ;



END $$ LANGUAGE plpgsql;
