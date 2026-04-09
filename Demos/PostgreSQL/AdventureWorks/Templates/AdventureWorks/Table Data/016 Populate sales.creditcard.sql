
DO $$
DECLARE
  v_json JSON = '{{sales.creditcard.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."creditcard" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'cardnumber')::varchar(25) AS "cardnumber",
           (elem ->> 'cardtype')::varchar(50) AS "cardtype",
           (elem ->> 'creditcardid')::int4 AS "creditcardid",
           (elem ->> 'expmonth')::int2 AS "expmonth",
           (elem ->> 'expyear')::int2 AS "expyear",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."creditcardid" = "Target"."creditcardid"

WHEN MATCHED AND (NOT ("Target"."cardnumber" = "Source"."cardnumber" OR ("Target"."cardnumber" IS NULL AND "Source"."cardnumber" IS NULL)) OR NOT ("Target"."cardtype" = "Source"."cardtype" OR ("Target"."cardtype" IS NULL AND "Source"."cardtype" IS NULL)) OR NOT ("Target"."expmonth" = "Source"."expmonth" OR ("Target"."expmonth" IS NULL AND "Source"."expmonth" IS NULL)) OR NOT ("Target"."expyear" = "Source"."expyear" OR ("Target"."expyear" IS NULL AND "Source"."expyear" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL))) THEN
  UPDATE SET
        "cardnumber" = "Source"."cardnumber",
        "cardtype" = "Source"."cardtype",
        "expmonth" = "Source"."expmonth",
        "expyear" = "Source"."expyear",
        "modifieddate" = "Source"."modifieddate"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "cardnumber",
        "cardtype",
        "creditcardid",
        "expmonth",
        "expyear",
        "modifieddate"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."cardnumber",
        "Source"."cardtype",
        "Source"."creditcardid",
        "Source"."expmonth",
        "Source"."expyear",
        "Source"."modifieddate"
   )
 ;

SELECT SETVAL('sales.creditcard_creditcardid_seq', (SELECT MAX("creditcardid") FROM "sales"."creditcard")) INTO nextval;

END $$ LANGUAGE plpgsql;
