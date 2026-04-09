-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

IF NOT EXISTS (SELECT * FROM SchemaSmith.TestLog WITH (NOLOCK) WHERE Msg = 'dbo.MyView.sql')
  RAISERROR('VIEW NOT FOUND', 16, 1)
ELSE
  INSERT SchemaSmith.TestLog (Msg) VALUES('dbo.FunctionThatNeedsView.sql')