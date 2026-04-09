
DO $$
DECLARE
  v_json JSON = '{{sales.specialoffer.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."specialoffer" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'category')::varchar(50) AS "category",
           (elem ->> 'description')::varchar(255) AS "description",
           (elem ->> 'discountpct')::numeric AS "discountpct",
           (elem ->> 'enddate')::timestamp(6) AS "enddate",
           (elem ->> 'maxqty')::int4 AS "maxqty",
           (elem ->> 'minqty')::int4 AS "minqty",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'specialofferid')::int4 AS "specialofferid",
           (elem ->> 'startdate')::timestamp(6) AS "startdate",
           (elem ->> 'type')::varchar(50) AS "type"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."specialofferid" = "Target"."specialofferid"

WHEN MATCHED AND (NOT ("Target"."category" = "Source"."category" OR ("Target"."category" IS NULL AND "Source"."category" IS NULL)) OR NOT ("Target"."description" = "Source"."description" OR ("Target"."description" IS NULL AND "Source"."description" IS NULL)) OR NOT ("Target"."discountpct" = "Source"."discountpct" OR ("Target"."discountpct" IS NULL AND "Source"."discountpct" IS NULL)) OR NOT ("Target"."enddate" = "Source"."enddate" OR ("Target"."enddate" IS NULL AND "Source"."enddate" IS NULL)) OR NOT ("Target"."maxqty" = "Source"."maxqty" OR ("Target"."maxqty" IS NULL AND "Source"."maxqty" IS NULL)) OR NOT ("Target"."minqty" = "Source"."minqty" OR ("Target"."minqty" IS NULL AND "Source"."minqty" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."startdate" = "Source"."startdate" OR ("Target"."startdate" IS NULL AND "Source"."startdate" IS NULL)) OR NOT ("Target"."type" = "Source"."type" OR ("Target"."type" IS NULL AND "Source"."type" IS NULL))) THEN
  UPDATE SET
        "category" = "Source"."category",
        "description" = "Source"."description",
        "discountpct" = "Source"."discountpct",
        "enddate" = "Source"."enddate",
        "maxqty" = "Source"."maxqty",
        "minqty" = "Source"."minqty",
        "modifieddate" = "Source"."modifieddate",
        "rowguid" = "Source"."rowguid",
        "startdate" = "Source"."startdate",
        "type" = "Source"."type"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "category",
        "description",
        "discountpct",
        "enddate",
        "maxqty",
        "minqty",
        "modifieddate",
        "rowguid",
        "specialofferid",
        "startdate",
        "type"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."category",
        "Source"."description",
        "Source"."discountpct",
        "Source"."enddate",
        "Source"."maxqty",
        "Source"."minqty",
        "Source"."modifieddate",
        "Source"."rowguid",
        "Source"."specialofferid",
        "Source"."startdate",
        "Source"."type"
   )
 ;

SELECT SETVAL('sales.specialoffer_specialofferid_seq', (SELECT MAX("specialofferid") FROM "sales"."specialoffer")) INTO nextval;

END $$ LANGUAGE plpgsql;
