
DECLARE @v_json NVARCHAR(MAX) = '{{Production.ProductReview.tabledata}}';


SET IDENTITY_INSERT [Production].[ProductReview] ON;
MERGE INTO [Production].[ProductReview] AS Target
USING (
  SELECT [Comments],[EmailAddress],[ModifiedDate],[ProductID],[ProductReviewID],[Rating],[ReviewDate],[ReviewerName]
    FROM OPENJSON(@v_json)
    WITH (
           [Comments] NVARCHAR(3850),
           [EmailAddress] NVARCHAR(50),
           [ModifiedDate] DATETIME,
           [ProductID] INT,
           [ProductReviewID] INT,
           [Rating] INT,
           [ReviewDate] DATETIME,
           [ReviewerName] NAME
    )
) AS Source
ON Source.[ProductReviewID] = Target.[ProductReviewID]

WHEN MATCHED AND (NOT (Target.[Comments] = Source.[Comments] OR (Target.[Comments] IS NULL AND Source.[Comments] IS NULL)) OR NOT (Target.[EmailAddress] = Source.[EmailAddress] OR (Target.[EmailAddress] IS NULL AND Source.[EmailAddress] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[ProductID] = Source.[ProductID] OR (Target.[ProductID] IS NULL AND Source.[ProductID] IS NULL)) OR NOT (Target.[Rating] = Source.[Rating] OR (Target.[Rating] IS NULL AND Source.[Rating] IS NULL)) OR NOT (Target.[ReviewDate] = Source.[ReviewDate] OR (Target.[ReviewDate] IS NULL AND Source.[ReviewDate] IS NULL)) OR NOT (Target.[ReviewerName] = Source.[ReviewerName] OR (Target.[ReviewerName] IS NULL AND Source.[ReviewerName] IS NULL))) THEN
  UPDATE SET
        [Comments] = Source.[Comments],
        [EmailAddress] = Source.[EmailAddress],
        [ModifiedDate] = Source.[ModifiedDate],
        [ProductID] = Source.[ProductID],
        [Rating] = Source.[Rating],
        [ReviewDate] = Source.[ReviewDate],
        [ReviewerName] = Source.[ReviewerName]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Comments],
        [EmailAddress],
        [ModifiedDate],
        [ProductID],
        [ProductReviewID],
        [Rating],
        [ReviewDate],
        [ReviewerName]
   ) VALUES (
         Source.[Comments],
        Source.[EmailAddress],
        Source.[ModifiedDate],
        Source.[ProductID],
        Source.[ProductReviewID],
        Source.[Rating],
        Source.[ReviewDate],
        Source.[ReviewerName]
   )
 ;
SET IDENTITY_INSERT [Production].[ProductReview] OFF;
