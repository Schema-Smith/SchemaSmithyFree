-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Create a marker table if this is the first run, insert a row to prove the
-- pre-sentinel work lands, then raise the sentinel to skip the migration.
CREATE TABLE IF NOT EXISTS public."SentinelMarker" (
    "Id" SERIAL PRIMARY KEY,
    "Marker" VARCHAR(100) NOT NULL
);

INSERT INTO public."SentinelMarker" ("Marker") VALUES ('sentinel-skip-test');

DO $$
BEGIN
  RAISE EXCEPTION 'SCHEMASMITH: SHOULD NOT APPLY';
END $$ LANGUAGE plpgsql;
