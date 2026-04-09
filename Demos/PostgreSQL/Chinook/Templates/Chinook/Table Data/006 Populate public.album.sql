
DO $$
DECLARE
  v_json JSON = '{{public.album.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."album" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'album_id')::int4 AS "album_id",
           (elem ->> 'artist_id')::int4 AS "artist_id",
           (elem ->> 'title')::varchar(160) AS "title"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."album_id" = "Target"."album_id"

WHEN MATCHED AND (NOT ("Target"."artist_id" = "Source"."artist_id" OR ("Target"."artist_id" IS NULL AND "Source"."artist_id" IS NULL)) OR NOT ("Target"."title" = "Source"."title" OR ("Target"."title" IS NULL AND "Source"."title" IS NULL))) THEN
  UPDATE SET
        "artist_id" = "Source"."artist_id",
        "title" = "Source"."title"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "album_id",
        "artist_id",
        "title"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."album_id",
        "Source"."artist_id",
        "Source"."title"
   )
 ;

SELECT SETVAL('public.album_album_id_seq', (SELECT MAX("album_id") FROM "public"."album")) INTO nextval;

END $$ LANGUAGE plpgsql;
