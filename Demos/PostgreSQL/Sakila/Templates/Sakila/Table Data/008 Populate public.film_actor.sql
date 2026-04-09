
DO $$
DECLARE
  v_json JSON = '{{public.film_actor.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."film_actor" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'actor_id')::int4 AS "actor_id",
           (elem ->> 'film_id')::int4 AS "film_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."actor_id" = "Target"."actor_id" AND "Source"."film_id" = "Target"."film_id"

WHEN MATCHED AND (NOT ("Target"."actor_id" = "Source"."actor_id" OR ("Target"."actor_id" IS NULL AND "Source"."actor_id" IS NULL)) OR NOT ("Target"."film_id" = "Source"."film_id" OR ("Target"."film_id" IS NULL AND "Source"."film_id" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL))) THEN
  UPDATE SET
        "actor_id" = "Source"."actor_id",
        "film_id" = "Source"."film_id",
        "last_update" = "Source"."last_update"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "actor_id",
        "film_id",
        "last_update"
   ) 
  VALUES (
         "Source"."actor_id",
        "Source"."film_id",
        "Source"."last_update"
   )
 ;



END $$ LANGUAGE plpgsql;
