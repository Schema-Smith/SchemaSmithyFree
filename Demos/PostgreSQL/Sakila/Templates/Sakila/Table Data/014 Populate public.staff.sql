
DO $$
DECLARE
  v_json JSON = '{{public.staff.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."staff" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'active')::bool AS "active",
           (elem ->> 'address_id')::int4 AS "address_id",
           (elem ->> 'email')::varchar(50) AS "email",
           (elem ->> 'first_name')::varchar(45) AS "first_name",
           (elem ->> 'last_name')::varchar(45) AS "last_name",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'password')::varchar(40) AS "password",
           decode(elem ->> 'picture', 'base64') AS "picture",
           (elem ->> 'staff_id')::int4 AS "staff_id",
           (elem ->> 'store_id')::int4 AS "store_id",
           (elem ->> 'username')::varchar(16) AS "username"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."staff_id" = "Target"."staff_id"

WHEN MATCHED AND (NOT ("Target"."active" = "Source"."active" OR ("Target"."active" IS NULL AND "Source"."active" IS NULL)) OR NOT ("Target"."address_id" = "Source"."address_id" OR ("Target"."address_id" IS NULL AND "Source"."address_id" IS NULL)) OR NOT ("Target"."email" = "Source"."email" OR ("Target"."email" IS NULL AND "Source"."email" IS NULL)) OR NOT ("Target"."first_name" = "Source"."first_name" OR ("Target"."first_name" IS NULL AND "Source"."first_name" IS NULL)) OR NOT ("Target"."last_name" = "Source"."last_name" OR ("Target"."last_name" IS NULL AND "Source"."last_name" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."password" = "Source"."password" OR ("Target"."password" IS NULL AND "Source"."password" IS NULL)) OR NOT ("Target"."picture" = "Source"."picture" OR ("Target"."picture" IS NULL AND "Source"."picture" IS NULL)) OR NOT ("Target"."store_id" = "Source"."store_id" OR ("Target"."store_id" IS NULL AND "Source"."store_id" IS NULL)) OR NOT ("Target"."username" = "Source"."username" OR ("Target"."username" IS NULL AND "Source"."username" IS NULL))) THEN
  UPDATE SET
        "active" = "Source"."active",
        "address_id" = "Source"."address_id",
        "email" = "Source"."email",
        "first_name" = "Source"."first_name",
        "last_name" = "Source"."last_name",
        "last_update" = "Source"."last_update",
        "password" = "Source"."password",
        "picture" = "Source"."picture",
        "store_id" = "Source"."store_id",
        "username" = "Source"."username"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "active",
        "address_id",
        "email",
        "first_name",
        "last_name",
        "last_update",
        "password",
        "picture",
        "staff_id",
        "store_id",
        "username"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."active",
        "Source"."address_id",
        "Source"."email",
        "Source"."first_name",
        "Source"."last_name",
        "Source"."last_update",
        "Source"."password",
        "Source"."picture",
        "Source"."staff_id",
        "Source"."store_id",
        "Source"."username"
   )
 ;

SELECT SETVAL('staff_staff_id_seq', (SELECT MAX("staff_id") FROM "public"."staff")) INTO nextval;

END $$ LANGUAGE plpgsql;
