
DO $$
DECLARE
  v_json JSON = '{{production.productsubcategory.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productsubcategory" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'productcategoryid')::int4 AS "productcategoryid",
           (elem ->> 'productsubcategoryid')::int4 AS "productsubcategoryid",
           (elem ->> 'rowguid')::uuid AS "rowguid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productsubcategoryid" = "Target"."productsubcategoryid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."productcategoryid" = "Source"."productcategoryid" OR ("Target"."productcategoryid" IS NULL AND "Source"."productcategoryid" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "productcategoryid" = "Source"."productcategoryid",
        "rowguid" = "Source"."rowguid"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "name",
        "productcategoryid",
        "productsubcategoryid",
        "rowguid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."modifieddate",
        "Source"."name",
        "Source"."productcategoryid",
        "Source"."productsubcategoryid",
        "Source"."rowguid"
   )
 ;

SELECT SETVAL('production.productsubcategory_productsubcategoryid_seq', (SELECT MAX("productsubcategoryid") FROM "production"."productsubcategory")) INTO nextval;

END $$ LANGUAGE plpgsql;
