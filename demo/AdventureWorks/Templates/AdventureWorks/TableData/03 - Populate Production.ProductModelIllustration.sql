DECLARE @v_json NVARCHAR(MAX) = '[
{"IllustrationID":3,"ModifiedDate":"2014-01-09T14:41:02.167","ProductModelID":7},
{"IllustrationID":3,"ModifiedDate":"2014-01-09T14:41:02.167","ProductModelID":10},
{"IllustrationID":4,"ModifiedDate":"2014-01-09T14:41:02.183","ProductModelID":47},
{"IllustrationID":5,"ModifiedDate":"2014-01-09T14:41:02.183","ProductModelID":47},
{"IllustrationID":4,"ModifiedDate":"2014-01-09T14:41:02.183","ProductModelID":48},
{"IllustrationID":5,"ModifiedDate":"2014-01-09T14:41:02.183","ProductModelID":48},
{"IllustrationID":6,"ModifiedDate":"2014-01-09T14:41:02.200","ProductModelID":67}
]';

 
MERGE INTO [Production].[ProductModelIllustration] AS Target
USING (
  SELECT [IllustrationID],[ModifiedDate],[ProductModelID]
    FROM OPENJSON(@v_json)
    WITH (
           [IllustrationID] INT,
           [ModifiedDate] DATETIME,
           [ProductModelID] INT
    )
) AS Source
ON Source.[ProductModelID] = Target.[ProductModelID] AND Source.[IllustrationID] = Target.[IllustrationID]
WHEN MATCHED AND (NOT (Target.[IllustrationID] = Source.[IllustrationID] OR (Target.[IllustrationID] IS NULL AND Source.[IllustrationID] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[ProductModelID] = Source.[ProductModelID] OR (Target.[ProductModelID] IS NULL AND Source.[ProductModelID] IS NULL))) THEN
  UPDATE SET
        [IllustrationID] = Source.[IllustrationID],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductModelID] = Source.[ProductModelID]
WHEN NOT MATCHED THEN
  INSERT (
        [IllustrationID],
        [ModifiedDate],
        [ProductModelID]
  ) VALUES (
        Source.[IllustrationID],
        Source.[ModifiedDate],
        Source.[ProductModelID]  
  );
 
