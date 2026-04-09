
DO $$
DECLARE
  v_json JSON = '{{public.employees.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."employees" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'address')::varchar(60) AS "address",
           (elem ->> 'birth_date')::date AS "birth_date",
           (elem ->> 'city')::varchar(15) AS "city",
           (elem ->> 'country')::varchar(15) AS "country",
           (elem ->> 'employee_id')::int2 AS "employee_id",
           (elem ->> 'extension')::varchar(4) AS "extension",
           (elem ->> 'first_name')::varchar(10) AS "first_name",
           (elem ->> 'hire_date')::date AS "hire_date",
           (elem ->> 'home_phone')::varchar(24) AS "home_phone",
           (elem ->> 'last_name')::varchar(20) AS "last_name",
           (elem ->> 'notes')::text AS "notes",
           decode(elem ->> 'photo', 'base64') AS "photo",
           (elem ->> 'photo_path')::varchar(255) AS "photo_path",
           (elem ->> 'postal_code')::varchar(10) AS "postal_code",
           (elem ->> 'region')::varchar(15) AS "region",
           (elem ->> 'reports_to')::int2 AS "reports_to",
           (elem ->> 'title')::varchar(30) AS "title",
           (elem ->> 'title_of_courtesy')::varchar(25) AS "title_of_courtesy"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."employee_id" = "Target"."employee_id"

WHEN MATCHED AND (NOT ("Target"."address" = "Source"."address" OR ("Target"."address" IS NULL AND "Source"."address" IS NULL)) OR NOT ("Target"."birth_date" = "Source"."birth_date" OR ("Target"."birth_date" IS NULL AND "Source"."birth_date" IS NULL)) OR NOT ("Target"."city" = "Source"."city" OR ("Target"."city" IS NULL AND "Source"."city" IS NULL)) OR NOT ("Target"."country" = "Source"."country" OR ("Target"."country" IS NULL AND "Source"."country" IS NULL)) OR NOT ("Target"."employee_id" = "Source"."employee_id" OR ("Target"."employee_id" IS NULL AND "Source"."employee_id" IS NULL)) OR NOT ("Target"."extension" = "Source"."extension" OR ("Target"."extension" IS NULL AND "Source"."extension" IS NULL)) OR NOT ("Target"."first_name" = "Source"."first_name" OR ("Target"."first_name" IS NULL AND "Source"."first_name" IS NULL)) OR NOT ("Target"."hire_date" = "Source"."hire_date" OR ("Target"."hire_date" IS NULL AND "Source"."hire_date" IS NULL)) OR NOT ("Target"."home_phone" = "Source"."home_phone" OR ("Target"."home_phone" IS NULL AND "Source"."home_phone" IS NULL)) OR NOT ("Target"."last_name" = "Source"."last_name" OR ("Target"."last_name" IS NULL AND "Source"."last_name" IS NULL)) OR NOT ("Target"."notes" = "Source"."notes" OR ("Target"."notes" IS NULL AND "Source"."notes" IS NULL)) OR NOT ("Target"."photo" = "Source"."photo" OR ("Target"."photo" IS NULL AND "Source"."photo" IS NULL)) OR NOT ("Target"."photo_path" = "Source"."photo_path" OR ("Target"."photo_path" IS NULL AND "Source"."photo_path" IS NULL)) OR NOT ("Target"."postal_code" = "Source"."postal_code" OR ("Target"."postal_code" IS NULL AND "Source"."postal_code" IS NULL)) OR NOT ("Target"."region" = "Source"."region" OR ("Target"."region" IS NULL AND "Source"."region" IS NULL)) OR NOT ("Target"."reports_to" = "Source"."reports_to" OR ("Target"."reports_to" IS NULL AND "Source"."reports_to" IS NULL)) OR NOT ("Target"."title" = "Source"."title" OR ("Target"."title" IS NULL AND "Source"."title" IS NULL)) OR NOT ("Target"."title_of_courtesy" = "Source"."title_of_courtesy" OR ("Target"."title_of_courtesy" IS NULL AND "Source"."title_of_courtesy" IS NULL))) THEN
  UPDATE SET
        "address" = "Source"."address",
        "birth_date" = "Source"."birth_date",
        "city" = "Source"."city",
        "country" = "Source"."country",
        "employee_id" = "Source"."employee_id",
        "extension" = "Source"."extension",
        "first_name" = "Source"."first_name",
        "hire_date" = "Source"."hire_date",
        "home_phone" = "Source"."home_phone",
        "last_name" = "Source"."last_name",
        "notes" = "Source"."notes",
        "photo" = "Source"."photo",
        "photo_path" = "Source"."photo_path",
        "postal_code" = "Source"."postal_code",
        "region" = "Source"."region",
        "reports_to" = "Source"."reports_to",
        "title" = "Source"."title",
        "title_of_courtesy" = "Source"."title_of_courtesy"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "address",
        "birth_date",
        "city",
        "country",
        "employee_id",
        "extension",
        "first_name",
        "hire_date",
        "home_phone",
        "last_name",
        "notes",
        "photo",
        "photo_path",
        "postal_code",
        "region",
        "reports_to",
        "title",
        "title_of_courtesy"
   ) 
  VALUES (
         "Source"."address",
        "Source"."birth_date",
        "Source"."city",
        "Source"."country",
        "Source"."employee_id",
        "Source"."extension",
        "Source"."first_name",
        "Source"."hire_date",
        "Source"."home_phone",
        "Source"."last_name",
        "Source"."notes",
        "Source"."photo",
        "Source"."photo_path",
        "Source"."postal_code",
        "Source"."region",
        "Source"."reports_to",
        "Source"."title",
        "Source"."title_of_courtesy"
   )
 ;



END $$ LANGUAGE plpgsql;
