
DECLARE @v_json NVARCHAR(MAX) = '{{Production.UnitMeasure.tabledata}}';



MERGE INTO [Production].[UnitMeasure] AS Target
USING (
  SELECT [ModifiedDate],[Name],[UnitMeasureCode]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [UnitMeasureCode] NCHAR(3)
    )
) AS Source
ON Source.[UnitMeasureCode] = Target.[UnitMeasureCode]

WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[UnitMeasureCode] = Source.[UnitMeasureCode] OR (Target.[UnitMeasureCode] IS NULL AND Source.[UnitMeasureCode] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [UnitMeasureCode] = Source.[UnitMeasureCode]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [ModifiedDate],
        [Name],
        [UnitMeasureCode]
   ) VALUES (
         Source.[ModifiedDate],
        Source.[Name],
        Source.[UnitMeasureCode]
   )
 ;
