
DECLARE @v_json NVARCHAR(MAX) = '{{Production.Illustration.tabledata}}';


SET IDENTITY_INSERT [Production].[Illustration] ON;
MERGE INTO [Production].[Illustration] AS Target
USING (
  SELECT [Diagram],[IllustrationID],[ModifiedDate]
    FROM OPENJSON(@v_json)
    WITH (
           [Diagram] XML,
           [IllustrationID] INT,
           [ModifiedDate] DATETIME
    )
) AS Source
ON Source.[IllustrationID] = Target.[IllustrationID]

WHEN MATCHED AND (NOT (CAST(Target.[Diagram] AS NVARCHAR(MAX)) = CAST(Source.[Diagram] AS NVARCHAR(MAX)) OR (Target.[Diagram] IS NULL AND Source.[Diagram] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL))) THEN
  UPDATE SET
        [Diagram] = Source.[Diagram],
        [ModifiedDate] = Source.[ModifiedDate]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Diagram],
        [IllustrationID],
        [ModifiedDate]
   ) VALUES (
         Source.[Diagram],
        Source.[IllustrationID],
        Source.[ModifiedDate]
   )
 ;
SET IDENTITY_INSERT [Production].[Illustration] OFF;
