
DO $$
DECLARE
  v_json JSON = '{{production.productreview.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "production"."productreview" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'comments')::varchar(3850) AS "comments",
           (elem ->> 'emailaddress')::varchar(50) AS "emailaddress",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'productid')::int4 AS "productid",
           (elem ->> 'productreviewid')::int4 AS "productreviewid",
           (elem ->> 'rating')::int4 AS "rating",
           (elem ->> 'reviewdate')::timestamp(6) AS "reviewdate",
           (elem ->> 'reviewername')::varchar(50) AS "reviewername"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."productreviewid" = "Target"."productreviewid"

WHEN MATCHED AND (NOT ("Target"."comments" = "Source"."comments" OR ("Target"."comments" IS NULL AND "Source"."comments" IS NULL)) OR NOT ("Target"."emailaddress" = "Source"."emailaddress" OR ("Target"."emailaddress" IS NULL AND "Source"."emailaddress" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."productid" = "Source"."productid" OR ("Target"."productid" IS NULL AND "Source"."productid" IS NULL)) OR NOT ("Target"."rating" = "Source"."rating" OR ("Target"."rating" IS NULL AND "Source"."rating" IS NULL)) OR NOT ("Target"."reviewdate" = "Source"."reviewdate" OR ("Target"."reviewdate" IS NULL AND "Source"."reviewdate" IS NULL)) OR NOT ("Target"."reviewername" = "Source"."reviewername" OR ("Target"."reviewername" IS NULL AND "Source"."reviewername" IS NULL))) THEN
  UPDATE SET
        "comments" = "Source"."comments",
        "emailaddress" = "Source"."emailaddress",
        "modifieddate" = "Source"."modifieddate",
        "productid" = "Source"."productid",
        "rating" = "Source"."rating",
        "reviewdate" = "Source"."reviewdate",
        "reviewername" = "Source"."reviewername"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "comments",
        "emailaddress",
        "modifieddate",
        "productid",
        "productreviewid",
        "rating",
        "reviewdate",
        "reviewername"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."comments",
        "Source"."emailaddress",
        "Source"."modifieddate",
        "Source"."productid",
        "Source"."productreviewid",
        "Source"."rating",
        "Source"."reviewdate",
        "Source"."reviewername"
   )
 ;

SELECT SETVAL('production.productreview_productreviewid_seq', (SELECT MAX("productreviewid") FROM "production"."productreview")) INTO nextval;

END $$ LANGUAGE plpgsql;
