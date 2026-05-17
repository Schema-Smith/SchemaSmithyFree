-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

IF NOT EXISTS (SELECT 1 FROM dbo.Lookup WHERE LookupID = 1)
    INSERT dbo.Lookup (LookupID, Code) VALUES (1, 'ALPHA');
