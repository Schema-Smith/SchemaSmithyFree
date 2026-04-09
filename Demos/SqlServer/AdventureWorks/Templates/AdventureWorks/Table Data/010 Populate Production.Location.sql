
DECLARE @v_json NVARCHAR(MAX) = '{{Production.Location.tabledata}}';


SET IDENTITY_INSERT [Production].[Location] ON;
MERGE INTO [Production].[Location] AS Target
USING (
  SELECT [Availability],[CostRate],[LocationID],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [Availability] DECIMAL(8, 2),
           [CostRate] SMALLMONEY,
           [LocationID] SMALLINT,
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[LocationID] = Target.[LocationID]

WHEN MATCHED AND (NOT (Target.[Availability] = Source.[Availability] OR (Target.[Availability] IS NULL AND Source.[Availability] IS NULL)) OR NOT (Target.[CostRate] = Source.[CostRate] OR (Target.[CostRate] IS NULL AND Source.[CostRate] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [Availability] = Source.[Availability],
        [CostRate] = Source.[CostRate],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Availability],
        [CostRate],
        [LocationID],
        [ModifiedDate],
        [Name]
   ) VALUES (
         Source.[Availability],
        Source.[CostRate],
        Source.[LocationID],
        Source.[ModifiedDate],
        Source.[Name]
   )
 ;
SET IDENTITY_INSERT [Production].[Location] OFF;
