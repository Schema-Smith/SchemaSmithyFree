
DO $$
DECLARE
  v_json JSON = '{{production.product.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."product" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'class')::bpchar(2) AS "class",
           (elem ->> 'color')::varchar(15) AS "color",
           (elem ->> 'daystomanufacture')::int4 AS "daystomanufacture",
           (elem ->> 'discontinueddate')::timestamp(6) AS "discontinueddate",
           (elem ->> 'finishedgoodsflag')::bool AS "finishedgoodsflag",
           (elem ->> 'listprice')::numeric AS "listprice",
           (elem ->> 'makeflag')::bool AS "makeflag",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'productline')::bpchar(2) AS "productline",
           (elem ->> 'productmodelid')::int4 AS "productmodelid",
           (elem ->> 'productnumber')::varchar(25) AS "productnumber",
           (elem ->> 'productsubcategoryid')::int4 AS "productsubcategoryid",
           (elem ->> 'reorderpoint')::int2 AS "reorderpoint",
           (elem ->> 'rowguid')::uuid AS "rowguid",
           (elem ->> 'safetystocklevel')::int2 AS "safetystocklevel",
           (elem ->> 'sellenddate')::timestamp(6) AS "sellenddate",
           (elem ->> 'sellstartdate')::timestamp(6) AS "sellstartdate",
           (elem ->> 'size')::varchar(5) AS "size",
           (elem ->> 'sizeunitmeasurecode')::bpchar(3) AS "sizeunitmeasurecode",
           (elem ->> 'standardcost')::numeric AS "standardcost",
           (elem ->> 'style')::bpchar(2) AS "style",
           (elem ->> 'weight')::numeric(8, 2) AS "weight",
           (elem ->> 'weightunitmeasurecode')::bpchar(3) AS "weightunitmeasurecode"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productid" = "Target"."productid"

WHEN MATCHED AND (NOT ("Target"."class" = "Source"."class" OR ("Target"."class" IS NULL AND "Source"."class" IS NULL)) OR NOT ("Target"."color" = "Source"."color" OR ("Target"."color" IS NULL AND "Source"."color" IS NULL)) OR NOT ("Target"."daystomanufacture" = "Source"."daystomanufacture" OR ("Target"."daystomanufacture" IS NULL AND "Source"."daystomanufacture" IS NULL)) OR NOT ("Target"."discontinueddate" = "Source"."discontinueddate" OR ("Target"."discontinueddate" IS NULL AND "Source"."discontinueddate" IS NULL)) OR NOT ("Target"."finishedgoodsflag" = "Source"."finishedgoodsflag" OR ("Target"."finishedgoodsflag" IS NULL AND "Source"."finishedgoodsflag" IS NULL)) OR NOT ("Target"."listprice" = "Source"."listprice" OR ("Target"."listprice" IS NULL AND "Source"."listprice" IS NULL)) OR NOT ("Target"."makeflag" = "Source"."makeflag" OR ("Target"."makeflag" IS NULL AND "Source"."makeflag" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."productline" = "Source"."productline" OR ("Target"."productline" IS NULL AND "Source"."productline" IS NULL)) OR NOT ("Target"."productmodelid" = "Source"."productmodelid" OR ("Target"."productmodelid" IS NULL AND "Source"."productmodelid" IS NULL)) OR NOT ("Target"."productnumber" = "Source"."productnumber" OR ("Target"."productnumber" IS NULL AND "Source"."productnumber" IS NULL)) OR NOT ("Target"."productsubcategoryid" = "Source"."productsubcategoryid" OR ("Target"."productsubcategoryid" IS NULL AND "Source"."productsubcategoryid" IS NULL)) OR NOT ("Target"."reorderpoint" = "Source"."reorderpoint" OR ("Target"."reorderpoint" IS NULL AND "Source"."reorderpoint" IS NULL)) OR NOT ("Target"."rowguid" = "Source"."rowguid" OR ("Target"."rowguid" IS NULL AND "Source"."rowguid" IS NULL)) OR NOT ("Target"."safetystocklevel" = "Source"."safetystocklevel" OR ("Target"."safetystocklevel" IS NULL AND "Source"."safetystocklevel" IS NULL)) OR NOT ("Target"."sellenddate" = "Source"."sellenddate" OR ("Target"."sellenddate" IS NULL AND "Source"."sellenddate" IS NULL)) OR NOT ("Target"."sellstartdate" = "Source"."sellstartdate" OR ("Target"."sellstartdate" IS NULL AND "Source"."sellstartdate" IS NULL)) OR NOT ("Target"."size" = "Source"."size" OR ("Target"."size" IS NULL AND "Source"."size" IS NULL)) OR NOT ("Target"."sizeunitmeasurecode" = "Source"."sizeunitmeasurecode" OR ("Target"."sizeunitmeasurecode" IS NULL AND "Source"."sizeunitmeasurecode" IS NULL)) OR NOT ("Target"."standardcost" = "Source"."standardcost" OR ("Target"."standardcost" IS NULL AND "Source"."standardcost" IS NULL)) OR NOT ("Target"."style" = "Source"."style" OR ("Target"."style" IS NULL AND "Source"."style" IS NULL)) OR NOT ("Target"."weight" = "Source"."weight" OR ("Target"."weight" IS NULL AND "Source"."weight" IS NULL)) OR NOT ("Target"."weightunitmeasurecode" = "Source"."weightunitmeasurecode" OR ("Target"."weightunitmeasurecode" IS NULL AND "Source"."weightunitmeasurecode" IS NULL))) THEN
  UPDATE SET
        "class" = "Source"."class",
        "color" = "Source"."color",
        "daystomanufacture" = "Source"."daystomanufacture",
        "discontinueddate" = "Source"."discontinueddate",
        "finishedgoodsflag" = "Source"."finishedgoodsflag",
        "listprice" = "Source"."listprice",
        "makeflag" = "Source"."makeflag",
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "productline" = "Source"."productline",
        "productmodelid" = "Source"."productmodelid",
        "productnumber" = "Source"."productnumber",
        "productsubcategoryid" = "Source"."productsubcategoryid",
        "reorderpoint" = "Source"."reorderpoint",
        "rowguid" = "Source"."rowguid",
        "safetystocklevel" = "Source"."safetystocklevel",
        "sellenddate" = "Source"."sellenddate",
        "sellstartdate" = "Source"."sellstartdate",
        "size" = "Source"."size",
        "sizeunitmeasurecode" = "Source"."sizeunitmeasurecode",
        "standardcost" = "Source"."standardcost",
        "style" = "Source"."style",
        "weight" = "Source"."weight",
        "weightunitmeasurecode" = "Source"."weightunitmeasurecode"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "class",
        "color",
        "daystomanufacture",
        "discontinueddate",
        "finishedgoodsflag",
        "listprice",
        "makeflag",
        "modifieddate",
        "name",
        "productid",
        "productline",
        "productmodelid",
        "productnumber",
        "productsubcategoryid",
        "reorderpoint",
        "rowguid",
        "safetystocklevel",
        "sellenddate",
        "sellstartdate",
        "size",
        "sizeunitmeasurecode",
        "standardcost",
        "style",
        "weight",
        "weightunitmeasurecode"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."class",
        "Source"."color",
        "Source"."daystomanufacture",
        "Source"."discontinueddate",
        "Source"."finishedgoodsflag",
        "Source"."listprice",
        "Source"."makeflag",
        "Source"."modifieddate",
        "Source"."name",
        "Source"."productid",
        "Source"."productline",
        "Source"."productmodelid",
        "Source"."productnumber",
        "Source"."productsubcategoryid",
        "Source"."reorderpoint",
        "Source"."rowguid",
        "Source"."safetystocklevel",
        "Source"."sellenddate",
        "Source"."sellstartdate",
        "Source"."size",
        "Source"."sizeunitmeasurecode",
        "Source"."standardcost",
        "Source"."style",
        "Source"."weight",
        "Source"."weightunitmeasurecode"
   )
 ;

SELECT SETVAL('production.product_productid_seq', (SELECT MAX("productid") FROM "production"."product")) INTO nextval;

END $$ LANGUAGE plpgsql;
