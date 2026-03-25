
DECLARE @v_json NVARCHAR(MAX) = '';

ALTER TABLE [dbo].[CustomerDemographics] DISABLE TRIGGER ALL;
 
MERGE INTO [dbo].[CustomerDemographics] AS Target
USING (
  SELECT [CustomerDesc],[CustomerTypeID]
    FROM OPENJSON(@v_json)
    WITH (
           [CustomerDesc] NVARCHAR(MAX),
           [CustomerTypeID] NCHAR(10)
    )
) AS Source
ON Source.[CustomerTypeID] = Target.[CustomerTypeID]


WHEN MATCHED AND (NOT (CAST(Target.[CustomerDesc] AS NVARCHAR(MAX)) = CAST(Source.[CustomerDesc] AS NVARCHAR(MAX)) OR (Target.[CustomerDesc] IS NULL AND Source.[CustomerDesc] IS NULL)) AND NOT (Target.[CustomerTypeID] = Source.[CustomerTypeID] OR (Target.[CustomerTypeID] IS NULL AND Source.[CustomerTypeID] IS NULL))) THEN
  UPDATE SET
        [CustomerDesc] = Source.[CustomerDesc],
        [CustomerTypeID] = Source.[CustomerTypeID]


WHEN NOT MATCHED THEN
  INSERT (
        [CustomerDesc],
        [CustomerTypeID]
  ) VALUES (
        Source.[CustomerDesc],
        Source.[CustomerTypeID]  
  )
;
ALTER TABLE [dbo].[CustomerDemographics] ENABLE TRIGGER ALL;
