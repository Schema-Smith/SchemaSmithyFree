-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Object script that raises the sentinel — tests that sentinel-skipping an object
-- script does not fail the run.
DO $$
BEGIN
  RAISE EXCEPTION 'SCHEMASMITH: SHOULD NOT APPLY';
END $$ LANGUAGE plpgsql;
