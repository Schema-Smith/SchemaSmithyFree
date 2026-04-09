
DO $$
DECLARE
  v_json JSON = '{{public.media_type.tabledata}}';
  nextval BIGINT;
BEGIN

MERGE INTO "public"."media_type" AS "Target"
USING (
    WITH my_tables(arr) AS (VALUES(v_json::JSON))
    SELECT (elem ->> 'media_type_id')::int4 AS "media_type_id",
           (elem ->> 'name')::varchar(120) AS "name"
      FROM my_tables, JSON_ARRAY_ELEMENTS(arr) AS elem
) AS "Source"
ON "Source"."media_type_id" = "Target"."media_type_id"

WHEN MATCHED AND (NOT ("Target"."name" = "Source"."name" OR ("Target"."name" IS NULL AND "Source"."name" IS NULL))) THEN
  UPDATE SET
        "name" = "Source"."name"

 WHEN NOT MATCHED THEN -- BY TARGET is optional in newer PostgreSQL versions only adding for clarity when DELETE is also used (requires PostgreSQL v17+)
   INSERT (
         "media_type_id",
        "name"
   ) OVERRIDING SYSTEM VALUE
  VALUES (
         "Source"."media_type_id",
        "Source"."name"
   )
 ;

SELECT SETVAL('public.media_type_media_type_id_seq', (SELECT MAX("media_type_id") FROM "public"."media_type")) INTO nextval;

END $$ LANGUAGE plpgsql;
