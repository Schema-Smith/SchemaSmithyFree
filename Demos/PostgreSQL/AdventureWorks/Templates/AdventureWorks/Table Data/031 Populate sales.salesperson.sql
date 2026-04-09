
DO $$
DECLARE
  v_json JSON = '{{sales.salesperson.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salesperson" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'bonus')::numeric AS "bonus",
           (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'commissionpct')::numeric AS "commissionpct",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'saleslastyear')::numeric AS "saleslastyear",
           (elem ->> 'salesquota')::numeric AS "salesquota",
           (elem ->> 'salesytd')::numeric AS "salesytd",
           (elem ->> 'territoryid')::int4 AS "territoryid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."businessentityid" = "Target"."businessentityid"

WHEN MATCHED AND (NOT ("Target"."bonus" = "Source"."bonus" OR ("Target"."bonus" IS NULL AND "Source"."bonus" IS NULL)) OR NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."commissionpct" = "Source"."commissionpct" OR ("Target"."commissionpct" IS NULL AND "Source"."commissionpct" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."saleslastyear" = "Source"."saleslastyear" OR ("Target"."saleslastyear" IS NULL AND "Source"."saleslastyear" IS NULL)) OR NOT ("Target"."salesquota" = "Source"."salesquota" OR ("Target"."salesquota" IS NULL AND "Source"."salesquota" IS NULL)) OR NOT ("Target"."salesytd" = "Source"."salesytd" OR ("Target"."salesytd" IS NULL AND "Source"."salesytd" IS NULL)) OR NOT ("Target"."territoryid" = "Source"."territoryid" OR ("Target"."territoryid" IS NULL AND "Source"."territoryid" IS NULL))) THEN
  UPDATE SET
        "bonus" = "Source"."bonus",
        "businessentityid" = "Source"."businessentityid",
        "commissionpct" = "Source"."commissionpct",
        "modifieddate" = "Source"."modifieddate",
        "rowguid" = "Source"."rowguid",
        "saleslastyear" = "Source"."saleslastyear",
        "salesquota" = "Source"."salesquota",
        "salesytd" = "Source"."salesytd",
        "territoryid" = "Source"."territoryid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "bonus",
        "businessentityid",
        "commissionpct",
        "modifieddate",
        "rowguid",
        "saleslastyear",
        "salesquota",
        "salesytd",
        "territoryid"
   ) 
  VALUES (
         "Source"."bonus",
        "Source"."businessentityid",
        "Source"."commissionpct",
        "Source"."modifieddate",
        "Source"."rowguid",
        "Source"."saleslastyear",
        "Source"."salesquota",
        "Source"."salesytd",
        "Source"."territoryid"
   )
 ;



END $$ LANGUAGE plpgsql;
