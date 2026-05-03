-- Copyright (c) SchemaSmith, LLC. All rights reserved.
-- Licensed for use and modification with SchemaSmith products only.
-- Redistribution outside of SchemaSmith product usage is prohibited.

DECLARE @TableData VARCHAR(MAX) = '{{TestTableData}}'

INSERT dbo.TestTable (DateCreated, ParentID, SomeText, [Status], TestID)
  SELECT DateCreated, ParentID, SomeText, [Status], TestID
    FROM OPENJSON(@TableData) WITH (
      [DateCreated] DATETIME '$.DateCreated',
      [SomeText] VARCHAR(2000) '$.SomeText',
      [ParentID] UNIQUEIDENTIFIER '$.ParentID',
      [TestID] UNIQUEIDENTIFIER '$.TestID',
      [Status] TINYINT '$.Status'
      ) t
    WHERE NOT EXISTS (SELECT * FROM {{TestQueryToken}}.dbo.TestTable tt WHERE tt.TestID = t.TestID)

