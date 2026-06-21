-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

-- Create a marker table if this is the first run, insert a row to prove the
-- pre-sentinel work lands, then raise the sentinel to skip the migration.
IF OBJECT_ID('dbo.SentinelMarker', 'U') IS NULL
    CREATE TABLE dbo.SentinelMarker (Id INT IDENTITY(1,1) NOT NULL, Marker NVARCHAR(100) NOT NULL);

INSERT INTO dbo.SentinelMarker (Marker) VALUES ('sentinel-skip-test');

RAISERROR('SCHEMASMITH: SHOULD NOT APPLY', 16, 1);
