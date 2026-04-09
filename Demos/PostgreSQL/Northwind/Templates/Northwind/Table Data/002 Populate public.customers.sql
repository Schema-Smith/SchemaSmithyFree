
DO $$
DECLARE
  v_json JSON = '{{public.customers.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."customers" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'address')::varchar(60) AS "address",
           (elem ->> 'city')::varchar(15) AS "city",
           (elem ->> 'company_name')::varchar(40) AS "company_name",
           (elem ->> 'contact_name')::varchar(30) AS "contact_name",
           (elem ->> 'contact_title')::varchar(30) AS "contact_title",
           (elem ->> 'country')::varchar(15) AS "country",
           (elem ->> 'customer_id')::varchar(5) AS "customer_id",
           (elem ->> 'fax')::varchar(24) AS "fax",
           (elem ->> 'phone')::varchar(24) AS "phone",
           (elem ->> 'postal_code')::varchar(10) AS "postal_code",
           (elem ->> 'region')::varchar(15) AS "region"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."customer_id" = "Target"."customer_id"

WHEN MATCHED AND (NOT ("Target"."address" = "Source"."address" OR ("Target"."address" IS NULL AND "Source"."address" IS NULL)) OR NOT ("Target"."city" = "Source"."city" OR ("Target"."city" IS NULL AND "Source"."city" IS NULL)) OR NOT ("Target"."company_name" = "Source"."company_name" OR ("Target"."company_name" IS NULL AND "Source"."company_name" IS NULL)) OR NOT ("Target"."contact_name" = "Source"."contact_name" OR ("Target"."contact_name" IS NULL AND "Source"."contact_name" IS NULL)) OR NOT ("Target"."contact_title" = "Source"."contact_title" OR ("Target"."contact_title" IS NULL AND "Source"."contact_title" IS NULL)) OR NOT ("Target"."country" = "Source"."country" OR ("Target"."country" IS NULL AND "Source"."country" IS NULL)) OR NOT ("Target"."customer_id" = "Source"."customer_id" OR ("Target"."customer_id" IS NULL AND "Source"."customer_id" IS NULL)) OR NOT ("Target"."fax" = "Source"."fax" OR ("Target"."fax" IS NULL AND "Source"."fax" IS NULL)) OR NOT ("Target"."phone" = "Source"."phone" OR ("Target"."phone" IS NULL AND "Source"."phone" IS NULL)) OR NOT ("Target"."postal_code" = "Source"."postal_code" OR ("Target"."postal_code" IS NULL AND "Source"."postal_code" IS NULL)) OR NOT ("Target"."region" = "Source"."region" OR ("Target"."region" IS NULL AND "Source"."region" IS NULL))) THEN
  UPDATE SET
        "address" = "Source"."address",
        "city" = "Source"."city",
        "company_name" = "Source"."company_name",
        "contact_name" = "Source"."contact_name",
        "contact_title" = "Source"."contact_title",
        "country" = "Source"."country",
        "customer_id" = "Source"."customer_id",
        "fax" = "Source"."fax",
        "phone" = "Source"."phone",
        "postal_code" = "Source"."postal_code",
        "region" = "Source"."region"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "address",
        "city",
        "company_name",
        "contact_name",
        "contact_title",
        "country",
        "customer_id",
        "fax",
        "phone",
        "postal_code",
        "region"
   ) 
  VALUES (
         "Source"."address",
        "Source"."city",
        "Source"."company_name",
        "Source"."contact_name",
        "Source"."contact_title",
        "Source"."country",
        "Source"."customer_id",
        "Source"."fax",
        "Source"."phone",
        "Source"."postal_code",
        "Source"."region"
   )
 ;



END $$ LANGUAGE plpgsql;
