
DO $$
DECLARE
  v_json JSON = '{{public.track.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."track" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'album_id')::int4 AS "album_id",
           (elem ->> 'bytes')::int4 AS "bytes",
           (elem ->> 'composer')::varchar(220) AS "composer",
           (elem ->> 'genre_id')::int4 AS "genre_id",
           (elem ->> 'media_type_id')::int4 AS "media_type_id",
           (elem ->> 'milliseconds')::int4 AS "milliseconds",
           (elem ->> 'name')::varchar(200) AS "name",
           (elem ->> 'track_id')::int4 AS "track_id",
           (elem ->> 'unit_price')::numeric(10, 2) AS "unit_price"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."track_id" = "Target"."track_id"

WHEN MATCHED AND (NOT ("Target"."album_id" = "Source"."album_id" OR ("Target"."album_id" IS NULL AND "Source"."album_id" IS NULL)) OR NOT ("Target"."bytes" = "Source"."bytes" OR ("Target"."bytes" IS NULL AND "Source"."bytes" IS NULL)) OR NOT ("Target"."composer" = "Source"."composer" OR ("Target"."composer" IS NULL AND "Source"."composer" IS NULL)) OR NOT ("Target"."genre_id" = "Source"."genre_id" OR ("Target"."genre_id" IS NULL AND "Source"."genre_id" IS NULL)) OR NOT ("Target"."media_type_id" = "Source"."media_type_id" OR ("Target"."media_type_id" IS NULL AND "Source"."media_type_id" IS NULL)) OR NOT ("Target"."milliseconds" = "Source"."milliseconds" OR ("Target"."milliseconds" IS NULL AND "Source"."milliseconds" IS NULL)) OR NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL)) OR NOT ("Target"."unit_price" = "Source"."unit_price" OR ("Target"."unit_price" IS NULL AND "Source"."unit_price" IS NULL))) THEN
  UPDATE SET
        "album_id" = "Source"."album_id",
        "bytes" = "Source"."bytes",
        "composer" = "Source"."composer",
        "genre_id" = "Source"."genre_id",
        "media_type_id" = "Source"."media_type_id",
        "milliseconds" = "Source"."milliseconds",
        "name" = "Source"."name",
        "unit_price" = "Source"."unit_price"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "album_id",
        "bytes",
        "composer",
        "genre_id",
        "media_type_id",
        "milliseconds",
        "name",
        "track_id",
        "unit_price"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."album_id",
        "Source"."bytes",
        "Source"."composer",
        "Source"."genre_id",
        "Source"."media_type_id",
        "Source"."milliseconds",
        "Source"."name",
        "Source"."track_id",
        "Source"."unit_price"
   )
 ;

SELECT SETVAL('public.track_track_id_seq', (SELECT MAX("track_id") FROM "public"."track")) INTO nextval;

END $$ LANGUAGE plpgsql;
