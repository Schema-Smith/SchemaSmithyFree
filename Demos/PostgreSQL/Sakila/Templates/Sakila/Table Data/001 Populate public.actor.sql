
DO $$
DECLARE
  v_json JSON = '{{public.actor.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."actor" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'actor_id')::int4 AS "actor_id",
           (elem ->> 'first_name')::varchar(45) AS "first_name",
           (elem ->> 'last_name')::varchar(45) AS "last_name",
           (elem ->> 'last_update')::timestamp(6) AS "last_update"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."actor_id" = "Target"."actor_id"

WHEN MATCHED AND (NOT ("Target"."first_name" = "Source"."first_name" OR ("Target"."first_name" IS NULL AND "Source"."first_name" IS NULL)) OR NOT ("Target"."last_name" = "Source"."last_name" OR ("Target"."last_name" IS NULL AND "Source"."last_name" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL))) THEN
  UPDATE SET
        "first_name" = "Source"."first_name",
        "last_name" = "Source"."last_name",
        "last_update" = "Source"."last_update"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "actor_id",
        "first_name",
        "last_name",
        "last_update"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."actor_id",
        "Source"."first_name",
        "Source"."last_name",
        "Source"."last_update"
   )
 ;

SELECT SETVAL('actor_actor_id_seq', (SELECT MAX("actor_id") FROM "public"."actor")) INTO nextval;

END $$ LANGUAGE plpgsql;
