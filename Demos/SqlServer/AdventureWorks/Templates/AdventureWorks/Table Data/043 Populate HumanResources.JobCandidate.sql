
DECLARE @v_json NVARCHAR(MAX) = '{{HumanResources.JobCandidate.tabledata}}';


SET IDENTITY_INSERT [HumanResources].[JobCandidate] ON;
MERGE INTO [HumanResources].[JobCandidate] AS Target
USING (
  SELECT [BusinessEntityID],[JobCandidateID],[ModifiedDate],[Resume]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [JobCandidateID] INT,
           [ModifiedDate] DATETIME,
           [Resume] XML([HumanResources].[HRResumeSchemaCollection])
    )
) AS Source
ON Source.[JobCandidateID] = Target.[JobCandidateID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (CAST(Target.[Resume] AS NVARCHAR(MAX)) = CAST(Source.[Resume] AS NVARCHAR(MAX)) OR (Target.[Resume] IS NULL AND Source.[Resume] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [ModifiedDate] = Source.[ModifiedDate],
        [Resume] = Source.[Resume]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [JobCandidateID],
        [ModifiedDate],
        [Resume]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[JobCandidateID],
        Source.[ModifiedDate],
        Source.[Resume]
   )
 ;
SET IDENTITY_INSERT [HumanResources].[JobCandidate] OFF;
