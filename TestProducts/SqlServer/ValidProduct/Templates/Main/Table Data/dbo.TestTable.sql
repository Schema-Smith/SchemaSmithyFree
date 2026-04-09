-- Copyright (c) SchemaSmith Contributors. Licensed under the SSCL v2.0.

DECLARE @TableData VARCHAR(MAX) = '{{TestTableInitialData}}'

INSERT dbo.TestTable (DateCreated, ParentID, SomeText, [Status], TestID)
  SELECT DateCreated, ParentID, SomeText, [Status], TestID
    FROM OPENJSON(@TableData) WITH (
      [DateCreated] DATETIME '$.DateCreated',
      [SomeText] VARCHAR(2000) '$.SomeText',
      [ParentID] UNIQUEIDENTIFIER '$.ParentID',
      [TestID] UNIQUEIDENTIFIER '$.TestID',
      [Status] TINYINT '$.Status'
      ) t
    WHERE NOT EXISTS (SELECT * FROM dbo.TestTable tt WHERE tt.TestID = t.TestID)
