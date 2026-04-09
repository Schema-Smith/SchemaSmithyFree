
DO $$
DECLARE
  v_json JSON = '{{public.invoice.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."invoice" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'billing_address')::varchar(70) AS "billing_address",
           (elem ->> 'billing_city')::varchar(40) AS "billing_city",
           (elem ->> 'billing_country')::varchar(40) AS "billing_country",
           (elem ->> 'billing_postal_code')::varchar(10) AS "billing_postal_code",
           (elem ->> 'billing_state')::varchar(40) AS "billing_state",
           (elem ->> 'customer_id')::int4 AS "customer_id",
           (elem ->> 'invoice_date')::timestamp(6) AS "invoice_date",
           (elem ->> 'invoice_id')::int4 AS "invoice_id",
           (elem ->> 'total')::numeric(10, 2) AS "total"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."invoice_id" = "Target"."invoice_id"

WHEN MATCHED AND (NOT ("Target"."billing_address" = "Source"."billing_address" OR ("Target"."billing_address" IS NULL AND "Source"."billing_address" IS NULL)) OR NOT ("Target"."billing_city" = "Source"."billing_city" OR ("Target"."billing_city" IS NULL AND "Source"."billing_city" IS NULL)) OR NOT ("Target"."billing_country" = "Source"."billing_country" OR ("Target"."billing_country" IS NULL AND "Source"."billing_country" IS NULL)) OR NOT ("Target"."billing_postal_code" = "Source"."billing_postal_code" OR ("Target"."billing_postal_code" IS NULL AND "Source"."billing_postal_code" IS NULL)) OR NOT ("Target"."billing_state" = "Source"."billing_state" OR ("Target"."billing_state" IS NULL AND "Source"."billing_state" IS NULL)) OR NOT ("Target"."customer_id" = "Source"."customer_id" OR ("Target"."customer_id" IS NULL AND "Source"."customer_id" IS NULL)) OR NOT ("Target"."invoice_date" = "Source"."invoice_date" OR ("Target"."invoice_date" IS NULL AND "Source"."invoice_date" IS NULL)) OR NOT ("Target"."total" = "Source"."total" OR ("Target"."total" IS NULL AND "Source"."total" IS NULL))) THEN
  UPDATE SET
        "billing_address" = "Source"."billing_address",
        "billing_city" = "Source"."billing_city",
        "billing_country" = "Source"."billing_country",
        "billing_postal_code" = "Source"."billing_postal_code",
        "billing_state" = "Source"."billing_state",
        "customer_id" = "Source"."customer_id",
        "invoice_date" = "Source"."invoice_date",
        "total" = "Source"."total"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "billing_address",
        "billing_city",
        "billing_country",
        "billing_postal_code",
        "billing_state",
        "customer_id",
        "invoice_date",
        "invoice_id",
        "total"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."billing_address",
        "Source"."billing_city",
        "Source"."billing_country",
        "Source"."billing_postal_code",
        "Source"."billing_state",
        "Source"."customer_id",
        "Source"."invoice_date",
        "Source"."invoice_id",
        "Source"."total"
   )
 ;

SELECT SETVAL('public.invoice_invoice_id_seq', (SELECT MAX("invoice_id") FROM "public"."invoice")) INTO nextval;

END $$ LANGUAGE plpgsql;
