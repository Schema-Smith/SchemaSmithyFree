
DO $$
DECLARE
  v_json JSON = '{{public.shippers.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."shippers" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'company_name')::varchar(40) AS "company_name",
           (elem ->> 'phone')::varchar(24) AS "phone",
           (elem ->> 'shipper_id')::int2 AS "shipper_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."shipper_id" = "Target"."shipper_id"

WHEN MATCHED AND (NOT ("Target"."company_name" = "Source"."company_name" OR ("Target"."company_name" IS NULL AND "Source"."company_name" IS NULL)) OR NOT ("Target"."phone" = "Source"."phone" OR ("Target"."phone" IS NULL AND "Source"."phone" IS NULL)) OR NOT ("Target"."shipper_id" = "Source"."shipper_id" OR ("Target"."shipper_id" IS NULL AND "Source"."shipper_id" IS NULL))) THEN
  UPDATE SET
        "company_name" = "Source"."company_name",
        "phone" = "Source"."phone",
        "shipper_id" = "Source"."shipper_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "company_name",
        "phone",
        "shipper_id"
   ) 
  VALUES (
         "Source"."company_name",
        "Source"."phone",
        "Source"."shipper_id"
   )
 ;



END $$ LANGUAGE plpgsql;
