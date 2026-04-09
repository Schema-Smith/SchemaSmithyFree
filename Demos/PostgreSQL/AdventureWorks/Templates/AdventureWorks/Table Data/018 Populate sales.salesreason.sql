
DO $$
DECLARE
  v_json JSON = '{{sales.salesreason.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "sales"."salesreason" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'modifieddate')::timestamp(6) AS "modifieddate",
           (elem ->> 'name')::varchar(50) AS "name",
           (elem ->> 'reasontype')::varchar(50) AS "reasontype",
           (elem ->> 'salesreasonid')::int4 AS "salesreasonid"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."salesreasonid" = "Target"."salesreasonid"

WHEN MATCHED AND (NOT ("Target"."modifieddate" = "Source"."modifieddate" OR ("Target"."modifieddate" IS NULL AND "Source"."modifieddate" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."reasontype" = "Source"."reasontype" OR ("Target"."reasontype" IS NULL AND "Source"."reasontype" IS NULL))) THEN
  UPDATE SET
        "modifieddate" = "Source"."modifieddate",
        "name" = "Source"."name",
        "reasontype" = "Source"."reasontype"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "modifieddate",
        "name",
        "reasontype",
        "salesreasonid"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."modifieddate",
        "Source"."name",
        "Source"."reasontype",
        "Source"."salesreasonid"
   )
 ;

SELECT SETVAL('sales.salesreason_salesreasonid_seq', (SELECT MAX("salesreasonid") FROM "sales"."salesreason")) INTO nextval;

END $$ LANGUAGE plpgsql;
