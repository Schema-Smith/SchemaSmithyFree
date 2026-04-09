ALTER TABLE "public"."payment" DISABLE RULE "payment_insert_p2007_01";
ALTER TABLE "public"."payment" DISABLE RULE "payment_insert_p2007_02";
ALTER TABLE "public"."payment" DISABLE RULE "payment_insert_p2007_03";
ALTER TABLE "public"."payment" DISABLE RULE "payment_insert_p2007_04";
ALTER TABLE "public"."payment" DISABLE RULE "payment_insert_p2007_05";
ALTER TABLE "public"."payment" DISABLE RULE "payment_insert_p2007_06";

DO $$
DECLARE
  v_json JSON = '{{public.payment.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."payment" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'amount')::numeric(5, 2) AS "amount",
           (elem ->> 'customer_id')::int4 AS "customer_id",
           (elem ->> 'payment_date')::timestamp(6) AS "payment_date",
           (elem ->> 'payment_id')::int4 AS "payment_id",
           (elem ->> 'rental_id')::int4 AS "rental_id",
           (elem ->> 'staff_id')::int4 AS "staff_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."payment_id" = "Target"."payment_id"

WHEN MATCHED AND (NOT ("Target"."amount" = "Source"."amount" OR ("Target"."amount" IS NULL AND "Source"."amount" IS NULL)) OR NOT ("Target"."customer_id" = "Source"."customer_id" OR ("Target"."customer_id" IS NULL AND "Source"."customer_id" IS NULL)) OR NOT ("Target"."payment_date" = "Source"."payment_date" OR ("Target"."payment_date" IS NULL AND "Source"."payment_date" IS NULL)) OR NOT ("Target"."rental_id" = "Source"."rental_id" OR ("Target"."rental_id" IS NULL AND "Source"."rental_id" IS NULL)) OR NOT ("Target"."staff_id" = "Source"."staff_id" OR ("Target"."staff_id" IS NULL AND "Source"."staff_id" IS NULL))) THEN
  UPDATE SET
        "amount" = "Source"."amount",
        "customer_id" = "Source"."customer_id",
        "payment_date" = "Source"."payment_date",
        "rental_id" = "Source"."rental_id",
        "staff_id" = "Source"."staff_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "amount",
        "customer_id",
        "payment_date",
        "payment_id",
        "rental_id",
        "staff_id"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."amount",
        "Source"."customer_id",
        "Source"."payment_date",
        "Source"."payment_id",
        "Source"."rental_id",
        "Source"."staff_id"
   )
 ;

SELECT SETVAL('payment_payment_id_seq', (SELECT MAX("payment_id") FROM "public"."payment")) INTO nextval;

END $$ LANGUAGE plpgsql;

ALTER TABLE "public"."payment" ENABLE RULE "payment_insert_p2007_01";
ALTER TABLE "public"."payment" ENABLE RULE "payment_insert_p2007_02";
ALTER TABLE "public"."payment" ENABLE RULE "payment_insert_p2007_03";
ALTER TABLE "public"."payment" ENABLE RULE "payment_insert_p2007_04";
ALTER TABLE "public"."payment" ENABLE RULE "payment_insert_p2007_05";
ALTER TABLE "public"."payment" ENABLE RULE "payment_insert_p2007_06";
