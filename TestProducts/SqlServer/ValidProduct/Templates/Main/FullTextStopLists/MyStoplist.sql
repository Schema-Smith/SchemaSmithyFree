-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

INSERT SchemaSmith.TestLog (Msg)
  VALUES('MyStoplist.sql')
GO
IF NOT EXISTS (SELECT * FROM sys.fulltext_stoplists ftsl WHERE ftsl.name = N'MyStopList')
BEGIN
  CREATE FULLTEXT STOPLIST [MyStopList];
  ALTER FULLTEXT STOPLIST [MyStopList] ADD '$' LANGUAGE 'Neutral';
END