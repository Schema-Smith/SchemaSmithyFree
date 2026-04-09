
DO $$
DECLARE
  v_json JSON = '{{public.genre.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."genre" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'genre_id')::int4 AS "genre_id",
           (elem ->> 'name')::varchar(120) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."genre_id" = "Target"."genre_id"

WHEN MATCHED AND (NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "genre_id",
        "name"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."genre_id",
        "Source"."name"
   )
 ;

SELECT SETVAL('public.genre_genre_id_seq', (SELECT MAX("genre_id") FROM "public"."genre")) INTO nextval;

END $$ LANGUAGE plpgsql;
