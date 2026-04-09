-- Column "fulltext" skipped: tsvector is not supported for data delivery

DO $$
DECLARE
  v_json JSON = '{{public.film.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."film" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'description')::text AS "description",
           (elem ->> 'film_id')::int4 AS "film_id",
           (elem ->> 'language_id')::int4 AS "language_id",
           (elem ->> 'last_update')::timestamp(6) AS "last_update",
           (elem ->> 'length')::int2 AS "length",
           (elem ->> 'original_language_id')::int4 AS "original_language_id",
           (elem ->> 'rating')::public.mpaa_rating AS "rating",
           (elem ->> 'release_year')::int4 AS "release_year",
           (elem ->> 'rental_duration')::int2 AS "rental_duration",
           (elem ->> 'rental_rate')::numeric(4, 2) AS "rental_rate",
           (elem ->> 'replacement_cost')::numeric(5, 2) AS "replacement_cost",
           STRING_TO_ARRAY((elem ->> 'special_features'), '*,*', '*NULL_VALUE_REPRESENTATION*')::_text AS "special_features",
           (elem ->> 'title')::varchar(255) AS "title"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."film_id" = "Target"."film_id"

WHEN MATCHED AND (NOT ("Target"."description" = "Source"."description" OR ("Target"."description" IS NULL AND "Source"."description" IS NULL)) OR NOT ("Target"."language_id" = "Source"."language_id" OR ("Target"."language_id" IS NULL AND "Source"."language_id" IS NULL)) OR NOT ("Target"."last_update" = "Source"."last_update" OR ("Target"."last_update" IS NULL AND "Source"."last_update" IS NULL)) OR NOT ("Target"."length" = "Source"."length" OR ("Target"."length" IS NULL AND "Source"."length" IS NULL)) OR NOT ("Target"."original_language_id" = "Source"."original_language_id" OR ("Target"."original_language_id" IS NULL AND "Source"."original_language_id" IS NULL)) OR NOT ("Target"."rating" = "Source"."rating" OR ("Target"."rating" IS NULL AND "Source"."rating" IS NULL)) OR NOT ("Target"."release_year" = "Source"."release_year" OR ("Target"."release_year" IS NULL AND "Source"."release_year" IS NULL)) OR NOT ("Target"."rental_duration" = "Source"."rental_duration" OR ("Target"."rental_duration" IS NULL AND "Source"."rental_duration" IS NULL)) OR NOT ("Target"."rental_rate" = "Source"."rental_rate" OR ("Target"."rental_rate" IS NULL AND "Source"."rental_rate" IS NULL)) OR NOT ("Target"."replacement_cost" = "Source"."replacement_cost" OR ("Target"."replacement_cost" IS NULL AND "Source"."replacement_cost" IS NULL)) OR NOT ("Target"."special_features" = "Source"."special_features" OR ("Target"."special_features" IS NULL AND "Source"."special_features" IS NULL)) OR NOT ("Target"."title" = "Source"."title" OR ("Target"."title" IS NULL AND "Source"."title" IS NULL))) THEN
  UPDATE SET
        "description" = "Source"."description",
        "language_id" = "Source"."language_id",
        "last_update" = "Source"."last_update",
        "length" = "Source"."length",
        "original_language_id" = "Source"."original_language_id",
        "rating" = "Source"."rating",
        "release_year" = "Source"."release_year",
        "rental_duration" = "Source"."rental_duration",
        "rental_rate" = "Source"."rental_rate",
        "replacement_cost" = "Source"."replacement_cost",
        "special_features" = "Source"."special_features",
        "title" = "Source"."title"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "description",
        "film_id",
        "language_id",
        "last_update",
        "length",
        "original_language_id",
        "rating",
        "release_year",
        "rental_duration",
        "rental_rate",
        "replacement_cost",
        "special_features",
        "title"
   ) OVERRIDING USER VALUE
  VALUES (
         "Source"."description",
        "Source"."film_id",
        "Source"."language_id",
        "Source"."last_update",
        "Source"."length",
        "Source"."original_language_id",
        "Source"."rating",
        "Source"."release_year",
        "Source"."rental_duration",
        "Source"."rental_rate",
        "Source"."replacement_cost",
        "Source"."special_features",
        "Source"."title"
   )
 ;

SELECT SETVAL('film_film_id_seq', (SELECT MAX("film_id") FROM "public"."film")) INTO nextval;

END $$ LANGUAGE plpgsql;
