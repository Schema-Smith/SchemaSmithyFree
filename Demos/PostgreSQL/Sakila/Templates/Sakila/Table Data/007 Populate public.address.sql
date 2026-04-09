
DO $$
DECLARE
  v_json JSON = '{{public.address.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."address" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'address')::varchar(50) AS "address",
           (elem ->> 'address2')::varchar(50) AS "address2",
           (elem ->> 'address_id')::int4 AS "address_id",
           (elem ->> 'city_id')::int4 AS "city_id",
           (elem ->> 'district')::varchar(20) AS "district",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'phone')::varchar(20) AS "phone",
           (elem ->> 'postal_code')::varchar(10) AS "postal_code"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."address_id" = "Target"."address_id"

WHEN MATCHED AND (NOT ("Target"."address" = "Source"."address" OR ("Target"."address" IS NULL AND "Source"."address" IS NULL)) OR NOT ("Target"."address2" = "Source"."address2" OR ("Target"."address2" IS NULL AND "Source"."address2" IS NULL)) OR NOT ("Target"."city_id" = "Source"."city_id" OR ("Target"."city_id" IS NULL AND "Source"."city_id" IS NULL)) OR NOT ("Target"."district" = "Source"."district" OR ("Target"."district" IS NULL AND "Source"."district" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."phone" = "Source"."phone" OR ("Target"."phone" IS NULL AND "Source"."phone" IS NULL)) OR NOT ("Target"."postal_code" = "Source"."postal_code" OR ("Target"."postal_code" IS NULL AND "Source"."postal_code" IS NULL))) THEN
  UPDATE SET
        "address" = "Source"."address",
        "address2" = "Source"."address2",
        "city_id" = "Source"."city_id",
        "district" = "Source"."district",
        "last_update" = "Source"."last_update",
        "phone" = "Source"."phone",
        "postal_code" = "Source"."postal_code"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "address",
        "address2",
        "address_id",
        "city_id",
        "district",
        "last_update",
        "phone",
        "postal_code"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."address",
        "Source"."address2",
        "Source"."address_id",
        "Source"."city_id",
        "Source"."district",
        "Source"."last_update",
        "Source"."phone",
        "Source"."postal_code"
   )
 ;

SELECT SETVAL('address_address_id_seq', (SELECT MAX("address_id") FROM "public"."address")) INTO nextval;

END $$ LANGUAGE plpgsql;
