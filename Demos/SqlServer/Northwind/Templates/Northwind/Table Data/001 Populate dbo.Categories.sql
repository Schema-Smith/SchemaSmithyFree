
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Categories.tabledata}}';


SET IDENTITY_INSERT [dbo].[Categories] ON;
MERGE INTO [dbo].[Categories] AS Target
USING (
  SELECT [CategoryID],[CategoryName],[Description],[Picture]
    FROM OPENJSON(@v_json)
    WITH (
           [CategoryID] INT,
           [CategoryName] NVARCHAR(15),
           [Description] NVARCHAR(MAX),
           [Picture] VARBINARY(MAX)
    )
) AS Source
ON Source.[CategoryID] = Target.[CategoryID]

WHEN MATCHED AND (NOT (Target.[CategoryName] = Source.[CategoryName] OR (Target.[CategoryName] IS NULL AND Source.[CategoryName] IS NULL)) OR NOT (CAST(Target.[Description] AS NVARCHAR(MAX)) = CAST(Source.[Description] AS NVARCHAR(MAX)) OR (Target.[Description] IS NULL AND Source.[Description] IS NULL)) OR NOT (CAST(Target.[Picture] AS VARBINARY(MAX)) = CAST(Source.[Picture] AS VARBINARY(MAX)) OR (Target.[Picture] IS NULL AND Source.[Picture] IS NULL))) THEN
  UPDATE SET
        [CategoryName] = Source.[CategoryName],
        [Description] = Source.[Description],
        [Picture] = Source.[Picture]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CategoryID],
        [CategoryName],
        [Description],
        [Picture]
   ) VALUES (
         Source.[CategoryID],
        Source.[CategoryName],
        Source.[Description],
        Source.[Picture]
   )
 ;
SET IDENTITY_INSERT [dbo].[Categories] OFF;
