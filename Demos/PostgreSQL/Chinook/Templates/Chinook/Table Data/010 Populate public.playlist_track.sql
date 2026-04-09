
DO $$
DECLARE
  v_json JSON = '{{public.playlist_track.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."playlist_track" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'playlist_id')::int4 AS "playlist_id",
           (elem ->> 'track_id')::int4 AS "track_id"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."playlist_id" = "Target"."playlist_id" AND "Source"."track_id" = "Target"."track_id"

WHEN MATCHED AND (NOT ("Target"."playlist_id" = "Source"."playlist_id" OR ("Target"."playlist_id" IS NULL AND "Source"."playlist_id" IS NULL)) OR NOT ("Target"."track_id" = "Source"."track_id" OR ("Target"."track_id" IS NULL AND "Source"."track_id" IS NULL))) THEN
  UPDATE SET
        "playlist_id" = "Source"."playlist_id",
        "track_id" = "Source"."track_id"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "playlist_id",
        "track_id"
   ) 
  VALUES (
         "Source"."playlist_id",
        "Source"."track_id"
   )
 ;



END $$ LANGUAGE plpgsql;
