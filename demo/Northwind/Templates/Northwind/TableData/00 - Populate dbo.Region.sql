
DECLARE @v_json NVARCHAR(MAX) = '[
{"RegionDescription":"Eastern                                           ","RegionID":1},
{"RegionDescription":"Western                                           ","RegionID":2},
{"RegionDescription":"Northern                                          ","RegionID":3},
{"RegionDescription":"Southern                                          ","RegionID":4}
]';

ALTER TABLE [dbo].[Region] DISABLE TRIGGER ALL;
 
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


WHEN MATCHED AND (NOT (Target.[RegionDescription] = Source.[RegionDescription] OR (Target.[RegionDescription] IS NULL AND Source.[RegionDescription] IS NULL)) AND NOT (Target.[RegionID] = Source.[RegionID] OR (Target.[RegionID] IS NULL AND Source.[RegionID] IS NULL))) THEN
  UPDATE SET
        [RegionDescription] = Source.[RegionDescription],
        [RegionID] = Source.[RegionID]


WHEN NOT MATCHED THEN
  INSERT (
        [RegionDescription],
        [RegionID]
  ) VALUES (
        Source.[RegionDescription],
        Source.[RegionID]  
  )
;
ALTER TABLE [dbo].[Region] ENABLE TRIGGER ALL;
