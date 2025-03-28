INSERT SchemaSmith.TestLog (Msg)
  VALUES('MyCatalog.sql')
GO
IF NOT EXISTS (SELECT * FROM sysfulltextcatalogs ftc WHERE ftc.name = N'MyCatalog')
CREATE FULLTEXT CATALOG [MyCatalog] 