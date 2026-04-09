
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Region.tabledata}}';



MERGE INTO [dbo].[Region] AS Target
USING (
  SELECT [RegionDescription],[RegionID]
    FROM OPENJSON(@v_json)
    WITH (
           [RegionDescription] NCHAR(50),
           [RegionID] INT
    )
) AS Source
ON Source.[RegionID] = Target.[RegionID]

WHEN MATCHED AND (NOT (Target.[RegionDescription] = Source.[RegionDescription] OR (Target.[RegionDescription] IS NULL AND Source.[RegionDescription] IS NULL)) OR NOT (Target.[RegionID] = Source.[RegionID] OR (Target.[RegionID] IS NULL AND Source.[RegionID] IS NULL))) THEN
  UPDATE SET
        [RegionDescription] = Source.[RegionDescription],
        [RegionID] = Source.[RegionID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [RegionDescription],
        [RegionID]
   ) VALUES (
         Source.[RegionDescription],
        Source.[RegionID]
   )
 ;
