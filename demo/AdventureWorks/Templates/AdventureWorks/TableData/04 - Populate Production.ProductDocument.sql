DECLARE @v_json NVARCHAR(MAX) = '[
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":317},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":318},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":319},
{"DocumentNode":"\/3\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":506},
{"DocumentNode":"\/3\/2\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":506},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":514},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":515},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":516},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":517},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":518},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":519},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":520},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":521},
{"DocumentNode":"\/3\/4\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":522},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":928},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":929},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":930},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":931},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":932},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":933},
{"DocumentNode":"\/2\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":934},
{"DocumentNode":"\/3\/3\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":935},
{"DocumentNode":"\/3\/3\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":936},
{"DocumentNode":"\/3\/3\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":937},
{"DocumentNode":"\/3\/3\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":938},
{"DocumentNode":"\/3\/3\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":939},
{"DocumentNode":"\/3\/3\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":940},
{"DocumentNode":"\/3\/3\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":941},
{"DocumentNode":"\/1\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":977},
{"DocumentNode":"\/1\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":997},
{"DocumentNode":"\/1\/1\/","ModifiedDate":"2013-12-29T13:51:58.103","ProductID":998},
{"DocumentNode":"\/1\/1\/","ModifiedDate":"2013-12-29T13:51:58.120","ProductID":999}
]';

 
MERGE INTO [Production].[ProductDocument] AS Target
USING (
  SELECT [DocumentNode],[ModifiedDate],[ProductID]
    FROM OPENJSON(@v_json)
    WITH (
           [DocumentNode] NVARCHAR(4000),
           [ModifiedDate] DATETIME,
           [ProductID] INT
    )
) AS Source
ON Source.[ProductID] = Target.[ProductID] AND Source.[DocumentNode] = Target.[DocumentNode]
WHEN MATCHED AND (NOT (Target.[DocumentNode] = Source.[DocumentNode] OR (Target.[DocumentNode] IS NULL AND Source.[DocumentNode] IS NULL)) AND NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL))) THEN
  UPDATE SET
        [DocumentNode] = Source.[DocumentNode],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID]
WHEN NOT MATCHED THEN
  INSERT (
        [DocumentNode],
        [ModifiedDate],
        [ProductID]
  ) VALUES (
        Source.[DocumentNode],
        Source.[ModifiedDate],
        Source.[ProductID]  
  );
 
