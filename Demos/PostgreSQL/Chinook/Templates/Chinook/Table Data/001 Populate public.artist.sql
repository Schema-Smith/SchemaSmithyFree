
DO $$
DECLARE
  v_json JSON = '{{public.artist.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."artist" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'artist_id')::int4 AS "artist_id",
           (elem ->> 'name')::varchar(120) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."artist_id" = "Target"."artist_id"

WHEN MATCHED AND (NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "artist_id",
        "name"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."artist_id",
        "Source"."name"
   )
 ;

SELECT SETVAL('public.artist_artist_id_seq', (SELECT MAX("artist_id") FROM "public"."artist")) INTO nextval;

END $$ LANGUAGE plpgsql;
