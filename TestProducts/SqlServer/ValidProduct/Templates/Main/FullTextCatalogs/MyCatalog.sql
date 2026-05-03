-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

INSERT SchemaSmith.TestLog (Msg)
  VALUES('MyCatalog.sql')
GO
IF NOT EXISTS (SELECT * FROM sysfulltextcatalogs ftc WHERE ftc.name = N'MyCatalog')
CREATE FULLTEXT CATALOG [MyCatalog] 