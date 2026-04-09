
DO $$
DECLARE
  v_json JSON = '{{sales.currencyrate.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."currencyrate" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'averagerate')::numeric AS "averagerate",
           (elem ->> 'currencyratedate')::timestamp(6) AS "currencyratedate",
           (elem ->> 'currencyrateid')::int4 AS "currencyrateid",
           (elem ->> 'endofdayrate')::numeric AS "endofdayrate",
           (elem ->> 'fromcurrencycode')::bpchar(3) AS "fromcurrencycode",
           (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'tocurrencycode')::bpchar(3) AS "tocurrencycode"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."currencyrateid" = "Target"."currencyrateid"

WHEN MATCHED AND (NOT ("Target"."averagerate" = "Source"."averagerate" OR ("Target"."averagerate" IS NULL AND "Source"."averagerate" IS NULL)) OR NOT ("Target"."currencyratedate" = "Source"."currencyratedate" OR ("Target"."currencyratedate" IS NULL AND "Source"."currencyratedate" IS NULL)) OR NOT ("Target"."endofdayrate" = "Source"."endofdayrate" OR ("Target"."endofdayrate" IS NULL AND "Source"."endofdayrate" IS NULL)) OR NOT ("Target"."fromcurrencycode" = "Source"."fromcurrencycode" OR ("Target"."fromcurrencycode" IS NULL AND "Source"."fromcurrencycode" IS NULL)) OR NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."tocurrencycode" = "Source"."tocurrencycode" OR ("Target"."tocurrencycode" IS NULL AND "Source"."tocurrencycode" IS NULL))) THEN
  UPDATE SET
        "averagerate" = "Source"."averagerate",
        "currencyratedate" = "Source"."currencyratedate",
        "endofdayrate" = "Source"."endofdayrate",
        "fromcurrencycode" = "Source"."fromcurrencycode",
        "modifieddate" = "Source"."modifieddate",
        "tocurrencycode" = "Source"."tocurrencycode"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "averagerate",
        "currencyratedate",
        "currencyrateid",
        "endofdayrate",
        "fromcurrencycode",
        "modifieddate",
        "tocurrencycode"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."averagerate",
        "Source"."currencyratedate",
        "Source"."currencyrateid",
        "Source"."endofdayrate",
        "Source"."fromcurrencycode",
        "Source"."modifieddate",
        "Source"."tocurrencycode"
   )
 ;

SELECT SETVAL('sales.currencyrate_currencyrateid_seq', (SELECT MAX("currencyrateid") FROM "sales"."currencyrate")) INTO nextval;

END $$ LANGUAGE plpgsql;
