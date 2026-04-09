
DO $$
DECLARE
  v_json JSON = '{{public.suppliers.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."suppliers" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'address')::varchar(60) AS "address",
           (elem ->> 'city')::varchar(15) AS "city",
           (elem ->> 'company_name')::varchar(40) AS "company_name",
           (elem ->> 'contact_name')::varchar(30) AS "contact_name",
           (elem ->> 'contact_title')::varchar(30) AS "contact_title",
           (elem ->> 'country')::varchar(15) AS "country",
           (elem ->> 'fax')::varchar(24) AS "fax",
           (elem ->> 'homepage')::text AS "homepage",
           (elem ->> 'phone')::varchar(24) AS "phone",
           (elem ->> 'postal_code')::varchar(10) AS "postal_code",
           (elem ->> 'region')::varchar(15) AS "region",
           (elem ->> 'supplier_id')::int2 AS "supplier_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."supplier_id" = "Target"."supplier_id"

WHEN MATCHED AND (NOT ("Target"."address" = "Source"."address" OR ("Target"."address" IS NULL AND "Source"."address" IS NULL)) OR NOT ("Target"."city" = "Source"."city" OR ("Target"."city" IS NULL AND "Source"."city" IS NULL)) OR NOT ("Target"."company_name" = "Source"."company_name" OR ("Target"."company_name" IS NULL AND "Source"."company_name" IS NULL)) OR NOT ("Target"."contact_name" = "Source"."contact_name" OR ("Target"."contact_name" IS NULL AND "Source"."contact_name" IS NULL)) OR NOT ("Target"."contact_title" = "Source"."contact_title" OR ("Target"."contact_title" IS NULL AND "Source"."contact_title" IS NULL)) OR NOT ("Target"."country" = "Source"."country" OR ("Target"."country" IS NULL AND "Source"."country" IS NULL)) OR NOT ("Target"."fax" = "Source"."fax" OR ("Target"."fax" IS NULL AND "Source"."fax" IS NULL)) OR NOT ("Target"."homepage" = "Source"."homepage" OR ("Target"."homepage" IS NULL AND "Source"."homepage" IS NULL)) OR NOT ("Target"."phone" = "Source"."phone" OR ("Target"."phone" IS NULL AND "Source"."phone" IS NULL)) OR NOT ("Target"."postal_code" = "Source"."postal_code" OR ("Target"."postal_code" IS NULL AND "Source"."postal_code" IS NULL)) OR NOT ("Target"."region" = "Source"."region" OR ("Target"."region" IS NULL AND "Source"."region" IS NULL)) OR NOT ("Target"."supplier_id" = "Source"."supplier_id" OR ("Target"."supplier_id" IS NULL AND "Source"."supplier_id" IS NULL))) THEN
  UPDATE SET
        "address" = "Source"."address",
        "city" = "Source"."city",
        "company_name" = "Source"."company_name",
        "contact_name" = "Source"."contact_name",
        "contact_title" = "Source"."contact_title",
        "country" = "Source"."country",
        "fax" = "Source"."fax",
        "homepage" = "Source"."homepage",
        "phone" = "Source"."phone",
        "postal_code" = "Source"."postal_code",
        "region" = "Source"."region",
        "supplier_id" = "Source"."supplier_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "address",
        "city",
        "company_name",
        "contact_name",
        "contact_title",
        "country",
        "fax",
        "homepage",
        "phone",
        "postal_code",
        "region",
        "supplier_id"
   ) 
  VALUES (
         "Source"."address",
        "Source"."city",
        "Source"."company_name",
        "Source"."contact_name",
        "Source"."contact_title",
        "Source"."country",
        "Source"."fax",
        "Source"."homepage",
        "Source"."phone",
        "Source"."postal_code",
        "Source"."region",
        "Source"."supplier_id"
   )
 ;



END $$ LANGUAGE plpgsql;
