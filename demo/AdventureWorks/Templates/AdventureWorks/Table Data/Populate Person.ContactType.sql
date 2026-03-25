
DECLARE @v_json NVARCHAR(MAX) = '[
{"ContactTypeID":1,"ModifiedDate":"2008-04-30T00:00:00","Name":"Accounting Manager"},
{"ContactTypeID":2,"ModifiedDate":"2008-04-30T00:00:00","Name":"Assistant Sales Agent"},
{"ContactTypeID":3,"ModifiedDate":"2008-04-30T00:00:00","Name":"Assistant Sales Representative"},
{"ContactTypeID":4,"ModifiedDate":"2008-04-30T00:00:00","Name":"Coordinator Foreign Markets"},
{"ContactTypeID":5,"ModifiedDate":"2008-04-30T00:00:00","Name":"Export Administrator"},
{"ContactTypeID":6,"ModifiedDate":"2008-04-30T00:00:00","Name":"International Marketing Manager"},
{"ContactTypeID":7,"ModifiedDate":"2008-04-30T00:00:00","Name":"Marketing Assistant"},
{"ContactTypeID":8,"ModifiedDate":"2008-04-30T00:00:00","Name":"Marketing Manager"},
{"ContactTypeID":9,"ModifiedDate":"2008-04-30T00:00:00","Name":"Marketing Representative"},
{"ContactTypeID":10,"ModifiedDate":"2008-04-30T00:00:00","Name":"Order Administrator"},
{"ContactTypeID":11,"ModifiedDate":"2008-04-30T00:00:00","Name":"Owner"},
{"ContactTypeID":12,"ModifiedDate":"2008-04-30T00:00:00","Name":"Owner\/Marketing Assistant"},
{"ContactTypeID":13,"ModifiedDate":"2008-04-30T00:00:00","Name":"Product Manager"},
{"ContactTypeID":14,"ModifiedDate":"2008-04-30T00:00:00","Name":"Purchasing Agent"},
{"ContactTypeID":15,"ModifiedDate":"2008-04-30T00:00:00","Name":"Purchasing Manager"},
{"ContactTypeID":16,"ModifiedDate":"2008-04-30T00:00:00","Name":"Regional Account Representative"},
{"ContactTypeID":17,"ModifiedDate":"2008-04-30T00:00:00","Name":"Sales Agent"},
{"ContactTypeID":18,"ModifiedDate":"2008-04-30T00:00:00","Name":"Sales Associate"},
{"ContactTypeID":19,"ModifiedDate":"2008-04-30T00:00:00","Name":"Sales Manager"},
{"ContactTypeID":20,"ModifiedDate":"2008-04-30T00:00:00","Name":"Sales Representative"}
]';

ALTER TABLE [Person].[ContactType] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [Person].[ContactType] ON; 
MERGE INTO [Person].[ContactType] AS Target
USING (
  SELECT [ContactTypeID],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [ContactTypeID] INT,
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[ContactTypeID] = Target.[ContactTypeID]


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
  INSERT (
        [ContactTypeID],
        [ModifiedDate],
        [Name]
  ) VALUES (
        Source.[ContactTypeID],
        Source.[ModifiedDate],
        Source.[Name]  
  )
;
SET IDENTITY_INSERT [Person].[ContactType] OFF;
ALTER TABLE [Person].[ContactType] ENABLE TRIGGER ALL;
