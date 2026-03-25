
DECLARE @v_json NVARCHAR(MAX) = '[
{"CultureID":"      ","ModifiedDate":"2008-04-30T00:00:00","Name":"Invariant Language (Invariant Country)"},
{"CultureID":"ar    ","ModifiedDate":"2008-04-30T00:00:00","Name":"Arabic"},
{"CultureID":"en    ","ModifiedDate":"2008-04-30T00:00:00","Name":"English"},
{"CultureID":"es    ","ModifiedDate":"2008-04-30T00:00:00","Name":"Spanish"},
{"CultureID":"fr    ","ModifiedDate":"2008-04-30T00:00:00","Name":"French"},
{"CultureID":"he    ","ModifiedDate":"2008-04-30T00:00:00","Name":"Hebrew"},
{"CultureID":"th    ","ModifiedDate":"2008-04-30T00:00:00","Name":"Thai"},
{"CultureID":"zh-cht","ModifiedDate":"2008-04-30T00:00:00","Name":"Chinese"}
]';

ALTER TABLE [Production].[Culture] DISABLE TRIGGER ALL;
 
MERGE INTO [Production].[Culture] AS Target
USING (
  SELECT [CultureID],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [CultureID] NCHAR(6),
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[CultureID] = Target.[CultureID]


WHEN MATCHED AND (NOT (Target.[CultureID] = Source.[CultureID] OR (Target.[CultureID] IS NULL AND Source.[CultureID] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [CultureID] = Source.[CultureID],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]


WHEN NOT MATCHED THEN
  INSERT (
        [CultureID],
        [ModifiedDate],
        [Name]
  ) VALUES (
        Source.[CultureID],
        Source.[ModifiedDate],
        Source.[Name]  
  )
;
ALTER TABLE [Production].[Culture] ENABLE TRIGGER ALL;
