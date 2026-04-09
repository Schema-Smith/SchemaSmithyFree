
DO $$
DECLARE
  v_json JSON = '{{public.invoice_line.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."invoice_line" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'invoice_id')::int4 AS "invoice_id",
           (elem ->> 'invoice_line_id')::int4 AS "invoice_line_id",
           (elem ->> 'quantity')::int4 AS "quantity",
           (elem ->> 'track_id')::int4 AS "track_id",
           (elem ->> 'unit_price')::numeric(10, 2) AS "unit_price"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."invoice_line_id" = "Target"."invoice_line_id"

WHEN MATCHED AND (NOT ("Target"."invoice_id" = "Source"."invoice_id" OR ("Target"."invoice_id" IS NULL AND "Source"."invoice_id" IS NULL)) OR NOT ("Target"."quantity" = "Source"."quantity" OR ("Target"."quantity" IS NULL AND "Source"."quantity" IS NULL)) OR NOT ("Target"."track_id" = "Source"."track_id" OR ("Target"."track_id" IS NULL AND "Source"."track_id" IS NULL)) OR NOT ("Target"."unit_price" = "Source"."unit_price" OR ("Target"."unit_price" IS NULL AND "Source"."unit_price" IS NULL))) THEN
  UPDATE SET
        "invoice_id" = "Source"."invoice_id",
        "quantity" = "Source"."quantity",
        "track_id" = "Source"."track_id",
        "unit_price" = "Source"."unit_price"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "invoice_id",
        "invoice_line_id",
        "quantity",
        "track_id",
        "unit_price"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."invoice_id",
        "Source"."invoice_line_id",
        "Source"."quantity",
        "Source"."track_id",
        "Source"."unit_price"
   )
 ;

SELECT SETVAL('public.invoice_line_invoice_line_id_seq', (SELECT MAX("invoice_line_id") FROM "public"."invoice_line")) INTO nextval;

END $$ LANGUAGE plpgsql;
