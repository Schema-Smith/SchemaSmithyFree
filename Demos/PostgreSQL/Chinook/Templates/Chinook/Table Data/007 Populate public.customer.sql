
DO $$
DECLARE
  v_json JSON = '{{public.customer.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."customer" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'address')::varchar(70) AS "address",
           (elem ->> 'city')::varchar(40) AS "city",
           (elem ->> 'company')::varchar(80) AS "company",
           (elem ->> 'country')::varchar(40) AS "country",
           (elem ->> 'customer_id')::int4 AS "customer_id",
           (elem ->> 'email')::varchar(60) AS "email",
           (elem ->> 'fax')::varchar(24) AS "fax",
           (elem ->> 'first_name')::varchar(40) AS "first_name",
           (elem ->> 'last_name')::varchar(20) AS "last_name",
           (elem ->> 'phone')::varchar(24) AS "phone",
           (elem ->> 'postal_code')::varchar(10) AS "postal_code",
           (elem ->> 'state')::varchar(40) AS "state",
           (elem ->> 'support_rep_id')::int4 AS "support_rep_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."customer_id" = "Target"."customer_id"

WHEN MATCHED AND (NOT ("Target"."address" = "Source"."address" OR ("Target"."address" IS NULL AND "Source"."address" IS NULL)) OR NOT ("Target"."city" = "Source"."city" OR ("Target"."city" IS NULL AND "Source"."city" IS NULL)) OR NOT ("Target"."company" = "Source"."company" OR ("Target"."company" IS NULL AND "Source"."company" IS NULL)) OR NOT ("Target"."country" = "Source"."country" OR ("Target"."country" IS NULL AND "Source"."country" IS NULL)) OR NOT ("Target"."email" = "Source"."email" OR ("Target"."email" IS NULL AND "Source"."email" IS NULL)) OR NOT ("Target"."fax" = "Source"."fax" OR ("Target"."fax" IS NULL AND "Source"."fax" IS NULL)) OR NOT ("Target"."first_name" = "Source"."first_name" OR ("Target"."first_name" IS NULL AND "Source"."first_name" IS NULL)) OR NOT ("Target"."last_name" = "Source"."last_name" OR ("Target"."last_name" IS NULL AND "Source"."last_name" IS NULL)) OR NOT ("Target"."phone" = "Source"."phone" OR ("Target"."phone" IS NULL AND "Source"."phone" IS NULL)) OR NOT ("Target"."postal_code" = "Source"."postal_code" OR ("Target"."postal_code" IS NULL AND "Source"."postal_code" IS NULL)) OR NOT ("Target"."state" = "Source"."state" OR ("Target"."state" IS NULL AND "Source"."state" IS NULL)) OR NOT ("Target"."support_rep_id" = "Source"."support_rep_id" OR ("Target"."support_rep_id" IS NULL AND "Source"."support_rep_id" IS NULL))) THEN
  UPDATE SET
        "address" = "Source"."address",
        "city" = "Source"."city",
        "company" = "Source"."company",
        "country" = "Source"."country",
        "email" = "Source"."email",
        "fax" = "Source"."fax",
        "first_name" = "Source"."first_name",
        "last_name" = "Source"."last_name",
        "phone" = "Source"."phone",
        "postal_code" = "Source"."postal_code",
        "state" = "Source"."state",
        "support_rep_id" = "Source"."support_rep_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "address",
        "city",
        "company",
        "country",
        "customer_id",
        "email",
        "fax",
        "first_name",
        "last_name",
        "phone",
        "postal_code",
        "state",
        "support_rep_id"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."address",
        "Source"."city",
        "Source"."company",
        "Source"."country",
        "Source"."customer_id",
        "Source"."email",
        "Source"."fax",
        "Source"."first_name",
        "Source"."last_name",
        "Source"."phone",
        "Source"."postal_code",
        "Source"."state",
        "Source"."support_rep_id"
   )
 ;

SELECT SETVAL('public.customer_customer_id_seq', (SELECT MAX("customer_id") FROM "public"."customer")) INTO nextval;

END $$ LANGUAGE plpgsql;
