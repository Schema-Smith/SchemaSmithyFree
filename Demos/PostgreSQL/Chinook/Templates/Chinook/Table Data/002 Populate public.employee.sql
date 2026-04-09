
DO $$
DECLARE
  v_json JSON = '{{public.employee.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."employee" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'address')::varchar(70) AS "address",
           (elem ->> 'birth_date')::timestamp(6) AS "birth_date",
           (elem ->> 'city')::varchar(40) AS "city",
           (elem ->> 'country')::varchar(40) AS "country",
           (elem ->> 'email')::varchar(60) AS "email",
           (elem ->> 'employee_id')::int4 AS "employee_id",
           (elem ->> 'fax')::varchar(24) AS "fax",
           (elem ->> 'first_name')::varchar(20) AS "first_name",
           (elem ->> 'hire_date')::timestamp(6) AS "hire_date",
           (elem ->> 'last_name')::varchar(20) AS "last_name",
           (elem ->> 'phone')::varchar(24) AS "phone",
           (elem ->> 'postal_code')::varchar(10) AS "postal_code",
           (elem ->> 'reports_to')::int4 AS "reports_to",
           (elem ->> 'state')::varchar(40) AS "state",
           (elem ->> 'title')::varchar(30) AS "title"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."employee_id" = "Target"."employee_id"

WHEN MATCHED AND (NOT ("Target"."address" = "Source"."address" OR ("Target"."address" IS NULL AND "Source"."address" IS NULL)) OR NOT ("Target"."birth_date" = "Source"."birth_date" OR ("Target"."birth_date" IS NULL AND "Source"."birth_date" IS NULL)) OR NOT ("Target"."city" = "Source"."city" OR ("Target"."city" IS NULL AND "Source"."city" IS NULL)) OR NOT ("Target"."country" = "Source"."country" OR ("Target"."country" IS NULL AND "Source"."country" IS NULL)) OR NOT ("Target"."email" = "Source"."email" OR ("Target"."email" IS NULL AND "Source"."email" IS NULL)) OR NOT ("Target"."fax" = "Source"."fax" OR ("Target"."fax" IS NULL AND "Source"."fax" IS NULL)) OR NOT ("Target"."first_name" = "Source"."first_name" OR ("Target"."first_name" IS NULL AND "Source"."first_name" IS NULL)) OR NOT ("Target"."hire_date" = "Source"."hire_date" OR ("Target"."hire_date" IS NULL AND "Source"."hire_date" IS NULL)) OR NOT ("Target"."last_name" = "Source"."last_name" OR ("Target"."last_name" IS NULL AND "Source"."last_name" IS NULL)) OR NOT ("Target"."phone" = "Source"."phone" OR ("Target"."phone" IS NULL AND "Source"."phone" IS NULL)) OR NOT ("Target"."postal_code" = "Source"."postal_code" OR ("Target"."postal_code" IS NULL AND "Source"."postal_code" IS NULL)) OR NOT ("Target"."reports_to" = "Source"."reports_to" OR ("Target"."reports_to" IS NULL AND "Source"."reports_to" IS NULL)) OR NOT ("Target"."state" = "Source"."state" OR ("Target"."state" IS NULL AND "Source"."state" IS NULL)) OR NOT ("Target"."title" = "Source"."title" OR ("Target"."title" IS NULL AND "Source"."title" IS NULL))) THEN
  UPDATE SET
        "address" = "Source"."address",
        "birth_date" = "Source"."birth_date",
        "city" = "Source"."city",
        "country" = "Source"."country",
        "email" = "Source"."email",
        "fax" = "Source"."fax",
        "first_name" = "Source"."first_name",
        "hire_date" = "Source"."hire_date",
        "last_name" = "Source"."last_name",
        "phone" = "Source"."phone",
        "postal_code" = "Source"."postal_code",
        "reports_to" = "Source"."reports_to",
        "state" = "Source"."state",
        "title" = "Source"."title"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "address",
        "birth_date",
        "city",
        "country",
        "email",
        "employee_id",
        "fax",
        "first_name",
        "hire_date",
        "last_name",
        "phone",
        "postal_code",
        "reports_to",
        "state",
        "title"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."address",
        "Source"."birth_date",
        "Source"."city",
        "Source"."country",
        "Source"."email",
        "Source"."employee_id",
        "Source"."fax",
        "Source"."first_name",
        "Source"."hire_date",
        "Source"."last_name",
        "Source"."phone",
        "Source"."postal_code",
        "Source"."reports_to",
        "Source"."state",
        "Source"."title"
   )
 ;

SELECT SETVAL('public.employee_employee_id_seq', (SELECT MAX("employee_id") FROM "public"."employee")) INTO nextval;

END $$ LANGUAGE plpgsql;
