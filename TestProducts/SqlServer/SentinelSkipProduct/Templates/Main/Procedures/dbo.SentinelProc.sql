-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Object script that raises the sentinel — tests that sentinel-skipping an object
-- script does not fail the run.
RAISERROR('SCHEMASMITH: SHOULD NOT APPLY', 16, 1);
