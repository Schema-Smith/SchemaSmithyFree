
DO $$
DECLARE
  v_json JSON = '{{purchasing.productvendor.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "purchasing"."productvendor" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'averageleadtime')::int4 AS "averageleadtime",
           (elem ->> 'businessentityid')::int4 AS "businessentityid",
           (elem ->> 'lastreceiptcost')::numeric AS "lastreceiptcost",
           (elem ->> 'lastreceiptdate')::timestamp(6) AS "lastreceiptdate",
           (elem ->> 'maxorderqty')::int4 AS "maxorderqty",
           (elem ->> 'minorderqty')::int4 AS "minorderqty",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'onorderqty')::int4 AS "onorderqty",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'standardprice')::numeric AS "standardprice",
           (elem ->> 'unitmeasurecode')::bpchar(3) AS "unitmeasurecode"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productid" = "Target"."productid" AND "Source"."businessentityid" = "Target"."businessentityid"

WHEN MATCHED AND (NOT ("Target"."averageleadtime" = "Source"."averageleadtime" OR ("Target"."averageleadtime" IS NULL AND "Source"."averageleadtime" IS NULL)) OR NOT ("Target"."businessentityid" = "Source"."businessentityid" OR ("Target"."businessentityid" IS NULL AND "Source"."businessentityid" IS NULL)) OR NOT ("Target"."lastreceiptcost" = "Source"."lastreceiptcost" OR ("Target"."lastreceiptcost" IS NULL AND "Source"."lastreceiptcost" IS NULL)) OR NOT ("Target"."lastreceiptdate" = "Source"."lastreceiptdate" OR ("Target"."lastreceiptdate" IS NULL AND "Source"."lastreceiptdate" IS NULL)) OR NOT ("Target"."maxorderqty" = "Source"."maxorderqty" OR ("Target"."maxorderqty" IS NULL AND "Source"."maxorderqty" IS NULL)) OR NOT ("Target"."minorderqty" = "Source"."minorderqty" OR ("Target"."minorderqty" IS NULL AND "Source"."minorderqty" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."onorderqty" = "Source"."onorderqty" OR ("Target"."onorderqty" IS NULL AND "Source"."onorderqty" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."standardprice" = "Source"."standardprice" OR ("Target"."standardprice" IS NULL AND "Source"."standardprice" IS NULL)) OR NOT ("Target"."unitmeasurecode" = "Source"."unitmeasurecode" OR ("Target"."unitmeasurecode" IS NULL AND "Source"."unitmeasurecode" IS NULL))) THEN
  UPDATE SET
        "averageleadtime" = "Source"."averageleadtime",
        "businessentityid" = "Source"."businessentityid",
        "lastreceiptcost" = "Source"."lastreceiptcost",
        "lastreceiptdate" = "Source"."lastreceiptdate",
        "maxorderqty" = "Source"."maxorderqty",
        "minorderqty" = "Source"."minorderqty",
        "modifieddate" = "Source"."modifieddate",
        "onorderqty" = "Source"."onorderqty",
        "productid" = "Source"."productid",
        "standardprice" = "Source"."standardprice",
        "unitmeasurecode" = "Source"."unitmeasurecode"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "averageleadtime",
        "businessentityid",
        "lastreceiptcost",
        "lastreceiptdate",
        "maxorderqty",
        "minorderqty",
        "modifieddate",
        "onorderqty",
        "productid",
        "standardprice",
        "unitmeasurecode"
   ) 
  VALUES (
         "Source"."averageleadtime",
        "Source"."businessentityid",
        "Source"."lastreceiptcost",
        "Source"."lastreceiptdate",
        "Source"."maxorderqty",
        "Source"."minorderqty",
        "Source"."modifieddate",
        "Source"."onorderqty",
        "Source"."productid",
        "Source"."standardprice",
        "Source"."unitmeasurecode"
   )
 ;



END $$ LANGUAGE plpgsql;
