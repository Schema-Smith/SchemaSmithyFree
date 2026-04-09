
DO $$
DECLARE
  v_json JSON = '{{public.playlist.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."playlist" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'name')::varchar(120) AS "name",
           (elem ->> 'playlist_id')::int4 AS "playlist_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."playlist_id" = "Target"."playlist_id"

WHEN MATCHED AND (NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "name",
        "playlist_id"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."name",
        "Source"."playlist_id"
   )
 ;

SELECT SETVAL('public.playlist_playlist_id_seq', (SELECT MAX("playlist_id") FROM "public"."playlist")) INTO nextval;

END $$ LANGUAGE plpgsql;
