
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ScrapReason.tabledata}}';


SET IDENTITY_INSERT [Production].[ScrapReason] ON;
MERGE INTO [Production].[ScrapReason] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ScrapReasonID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ScrapReasonID] SMALLINT
    )
) AS Source
ON Source.[ScrapReasonID] = Target.[ScrapReasonID]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [ScrapReasonID]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[ScrapReasonID]
   )
 ;
SET IDENTITY_INSERT [Production].[ScrapReason] OFF;
