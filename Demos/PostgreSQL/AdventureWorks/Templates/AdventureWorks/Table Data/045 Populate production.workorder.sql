
DO $$
DECLARE
  v_json JSON = '{{production.workorder.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."workorder" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'duedate')::timestamp(6) AS "duedate",
           (elem ->> 'enddate')::timestamp(6) AS "enddate",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'orderqty')::int4 AS "orderqty",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'scrappedqty')::int2 AS "scrappedqty",
           (elem ->> 'scrapreasonid')::int2 AS "scrapreasonid",
           (elem ->> 'startdate')::timestamp(6) AS "startdate",
           (elem ->> 'workorderid')::int4 AS "workorderid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."workorderid" = "Target"."workorderid"

WHEN MATCHED AND (NOT ("Target"."duedate" = "Source"."duedate" OR ("Target"."duedate" IS NULL AND "Source"."duedate" IS NULL)) OR NOT ("Target"."enddate" = "Source"."enddate" OR ("Target"."enddate" IS NULL AND "Source"."enddate" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."orderqty" = "Source"."orderqty" OR ("Target"."orderqty" IS NULL AND "Source"."orderqty" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."scrappedqty" = "Source"."scrappedqty" OR ("Target"."scrappedqty" IS NULL AND "Source"."scrappedqty" IS NULL)) OR NOT ("Target"."scrapreasonid" = "Source"."scrapreasonid" OR ("Target"."scrapreasonid" IS NULL AND "Source"."scrapreasonid" IS NULL)) OR NOT ("Target"."startdate" = "Source"."startdate" OR ("Target"."startdate" IS NULL AND "Source"."startdate" IS NULL))) THEN
  UPDATE SET
        "duedate" = "Source"."duedate",
        "enddate" = "Source"."enddate",
        "modifieddate" = "Source"."modifieddate",
        "orderqty" = "Source"."orderqty",
        "productid" = "Source"."productid",
        "scrappedqty" = "Source"."scrappedqty",
        "scrapreasonid" = "Source"."scrapreasonid",
        "startdate" = "Source"."startdate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "duedate",
        "enddate",
        "modifieddate",
        "orderqty",
        "productid",
        "scrappedqty",
        "scrapreasonid",
        "startdate",
        "workorderid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."duedate",
        "Source"."enddate",
        "Source"."modifieddate",
        "Source"."orderqty",
        "Source"."productid",
        "Source"."scrappedqty",
        "Source"."scrapreasonid",
        "Source"."startdate",
        "Source"."workorderid"
   )
 ;

SELECT SETVAL('production.workorder_workorderid_seq', (SELECT MAX("workorderid") FROM "production"."workorder")) INTO nextval;

END $$ LANGUAGE plpgsql;
