INSERT SchemaSmith.TestLog (Msg)
  VALUES('MyStoplist.sql')
GO
IF NOT EXISTS (SELECT * FROM sys.fulltext_stoplists ftsl WHERE ftsl.name = N'MyStopList')
BEGIN
  CREATE FULLTEXT STOPLIST [MyStopList];
  ALTER FULLTEXT STOPLIST [MyStopList] ADD '$' LANGUAGE 'Neutral';
END