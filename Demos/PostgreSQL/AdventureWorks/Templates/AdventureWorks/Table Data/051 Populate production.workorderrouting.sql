
DO $$
DECLARE
  v_json JSON = '{{production.workorderrouting.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."workorderrouting" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'actualcost')::numeric AS "actualcost",
           (elem ->> 'actualenddate')::timestamp(6) AS "actualenddate",
           (elem ->> 'actualresourcehrs')::numeric(9, 4) AS "actualresourcehrs",
           (elem ->> 'actualstartdate')::timestamp(6) AS "actualstartdate",
           (elem ->> 'locationid')::int2 AS "locationid",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'operationsequence')::int2 AS "operationsequence",
           (elem ->> 'plannedcost')::numeric AS "plannedcost",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'scheduledenddate')::timestamp(6) AS "scheduledenddate",
           (elem ->> 'scheduledstartdate')::timestamp(6) AS "scheduledstartdate",
           (elem ->> 'workorderid')::int4 AS "workorderid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."workorderid" = "Target"."workorderid" AND "Source"."productid" = "Target"."productid" AND "Source"."operationsequence" = "Target"."operationsequence"

WHEN MATCHED AND (NOT ("Target"."actualcost" = "Source"."actualcost" OR ("Target"."actualcost" IS NULL AND "Source"."actualcost" IS NULL)) OR NOT ("Target"."actualenddate" = "Source"."actualenddate" OR ("Target"."actualenddate" IS NULL AND "Source"."actualenddate" IS NULL)) OR NOT ("Target"."actualresourcehrs" = "Source"."actualresourcehrs" OR ("Target"."actualresourcehrs" IS NULL AND "Source"."actualresourcehrs" IS NULL)) OR NOT ("Target"."actualstartdate" = "Source"."actualstartdate" OR ("Target"."actualstartdate" IS NULL AND "Source"."actualstartdate" IS NULL)) OR NOT ("Target"."locationid" = "Source"."locationid" OR ("Target"."locationid" IS NULL AND "Source"."locationid" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."operationsequence" = "Source"."operationsequence" OR ("Target"."operationsequence" IS NULL AND "Source"."operationsequence" IS NULL)) OR NOT ("Target"."plannedcost" = "Source"."plannedcost" OR ("Target"."plannedcost" IS NULL AND "Source"."plannedcost" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."scheduledenddate" = "Source"."scheduledenddate" OR ("Target"."scheduledenddate" IS NULL AND "Source"."scheduledenddate" IS NULL)) OR NOT ("Target"."scheduledstartdate" = "Source"."scheduledstartdate" OR ("Target"."scheduledstartdate" IS NULL AND "Source"."scheduledstartdate" IS NULL)) OR NOT ("Target"."workorderid" = "Source"."workorderid" OR ("Target"."workorderid" IS NULL AND "Source"."workorderid" IS NULL))) THEN
  UPDATE SET
        "actualcost" = "Source"."actualcost",
        "actualenddate" = "Source"."actualenddate",
        "actualresourcehrs" = "Source"."actualresourcehrs",
        "actualstartdate" = "Source"."actualstartdate",
        "locationid" = "Source"."locationid",
        "modifieddate" = "Source"."modifieddate",
        "operationsequence" = "Source"."operationsequence",
        "plannedcost" = "Source"."plannedcost",
        "productid" = "Source"."productid",
        "scheduledenddate" = "Source"."scheduledenddate",
        "scheduledstartdate" = "Source"."scheduledstartdate",
        "workorderid" = "Source"."workorderid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "actualcost",
        "actualenddate",
        "actualresourcehrs",
        "actualstartdate",
        "locationid",
        "modifieddate",
        "operationsequence",
        "plannedcost",
        "productid",
        "scheduledenddate",
        "scheduledstartdate",
        "workorderid"
   ) 
  VALUES (
         "Source"."actualcost",
        "Source"."actualenddate",
        "Source"."actualresourcehrs",
        "Source"."actualstartdate",
        "Source"."locationid",
        "Source"."modifieddate",
        "Source"."operationsequence",
        "Source"."plannedcost",
        "Source"."productid",
        "Source"."scheduledenddate",
        "Source"."scheduledstartdate",
        "Source"."workorderid"
   )
 ;



END $$ LANGUAGE plpgsql;
