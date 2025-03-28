IF NOT EXISTS (SELECT * FROM SchemaSmith.TestLog WITH (NOLOCK) WHERE Msg = 'dbo.MyView.sql')
  RAISERROR('VIEW NOT FOUND', 16, 1)
ELSE
  INSERT SchemaSmith.TestLog (Msg) VALUES('dbo.FunctionThatNeedsView.sql')