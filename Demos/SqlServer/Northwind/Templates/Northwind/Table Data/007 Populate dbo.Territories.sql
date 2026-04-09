
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Territories.tabledata}}';



MERGE INTO [dbo].[Territories] AS Target
USING (
  SELECT [RegionID],[TerritoryDescription],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [RegionID] INT,
           [TerritoryDescription] NCHAR(50),
           [TerritoryID] NVARCHAR(20)
    )
) AS Source
ON Source.[TerritoryID] = Target.[TerritoryID]

WHEN MATCHED AND (NOT (Target.[RegionID] = Source.[RegionID] OR (Target.[RegionID] IS NULL AND Source.[RegionID] IS NULL)) OR NOT (Target.[TerritoryDescription] = Source.[TerritoryDescription] OR (Target.[TerritoryDescription] IS NULL AND Source.[TerritoryDescription] IS NULL)) OR NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [RegionID] = Source.[RegionID],
        [TerritoryDescription] = Source.[TerritoryDescription],
        [TerritoryID] = Source.[TerritoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [RegionID],
        [TerritoryDescription],
        [TerritoryID]
   ) VALUES (
         Source.[RegionID],
        Source.[TerritoryDescription],
        Source.[TerritoryID]
   )
 ;
