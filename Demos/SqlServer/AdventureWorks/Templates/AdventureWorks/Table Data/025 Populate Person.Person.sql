
DECLARE @v_json NVARCHAR(MAX) = '{{Person.Person.tabledata}}';



MERGE INTO [Person].[Person] AS Target
USING (
  SELECT [AdditionalContactInfo],[BusinessEntityID],[Demographics],[EmailPromotion],[FirstName],[LastName],[MiddleName],[ModifiedDate],[NameStyle],[PersonType],[Suffix],[Title]
    FROM OPENJSON(@v_json)
    WITH (
           [AdditionalContactInfo] XML([Person].[AdditionalContactInfoSchemaCollection]),
           [BusinessEntityID] INT,
           [Demographics] XML([Person].[IndividualSurveySchemaCollection]),
           [EmailPromotion] INT,
           [FirstName] NAME,
           [LastName] NAME,
           [MiddleName] NAME,
           [ModifiedDate] DATETIME,
           [NameStyle] NAMESTYLE,
           [PersonType] NCHAR(2),
           [rowguid] UNIQUEIDENTIFIER,
           [Suffix] NVARCHAR(10),
           [Title] NVARCHAR(8)
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (CAST(Target.[AdditionalContactInfo] AS NVARCHAR(MAX)) = CAST(Source.[AdditionalContactInfo] AS NVARCHAR(MAX)) OR (Target.[AdditionalContactInfo] IS NULL AND Source.[AdditionalContactInfo] IS NULL)) OR NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (CAST(Target.[Demographics] AS NVARCHAR(MAX)) = CAST(Source.[Demographics] AS NVARCHAR(MAX)) OR (Target.[Demographics] IS NULL AND Source.[Demographics] IS NULL)) OR NOT (Target.[EmailPromotion] = Source.[EmailPromotion] OR (Target.[EmailPromotion] IS NULL AND Source.[EmailPromotion] IS NULL)) OR NOT (Target.[FirstName] = Source.[FirstName] OR (Target.[FirstName] IS NULL AND Source.[FirstName] IS NULL)) OR NOT (Target.[LastName] = Source.[LastName] OR (Target.[LastName] IS NULL AND Source.[LastName] IS NULL)) OR NOT (Target.[MiddleName] = Source.[MiddleName] OR (Target.[MiddleName] IS NULL AND Source.[MiddleName] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[NameStyle] = Source.[NameStyle] OR (Target.[NameStyle] IS NULL AND Source.[NameStyle] IS NULL)) OR NOT (Target.[PersonType] = Source.[PersonType] OR (Target.[PersonType] IS NULL AND Source.[PersonType] IS NULL)) OR NOT (Target.[Suffix] = Source.[Suffix] OR (Target.[Suffix] IS NULL AND Source.[Suffix] IS NULL)) OR NOT (Target.[Title] = Source.[Title] OR (Target.[Title] IS NULL AND Source.[Title] IS NULL))) THEN
  UPDATE SET
        [AdditionalContactInfo] = Source.[AdditionalContactInfo],
        [BusinessEntityID] = Source.[BusinessEntityID],
        [Demographics] = Source.[Demographics],
        [EmailPromotion] = Source.[EmailPromotion],
        [FirstName] = Source.[FirstName],
        [LastName] = Source.[LastName],
        [MiddleName] = Source.[MiddleName],
        [ModifiedDate] = Source.[ModifiedDate],
        [NameStyle] = Source.[NameStyle],
        [PersonType] = Source.[PersonType],
        [Suffix] = Source.[Suffix],
        [Title] = Source.[Title]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AdditionalContactInfo],
        [BusinessEntityID],
        [Demographics],
        [EmailPromotion],
        [FirstName],
        [LastName],
        [MiddleName],
        [ModifiedDate],
        [NameStyle],
        [PersonType],
        [Suffix],
        [Title]
   ) VALUES (
         Source.[AdditionalContactInfo],
        Source.[BusinessEntityID],
        Source.[Demographics],
        Source.[EmailPromotion],
        Source.[FirstName],
        Source.[LastName],
        Source.[MiddleName],
        Source.[ModifiedDate],
        Source.[NameStyle],
        Source.[PersonType],
        Source.[Suffix],
        Source.[Title]
   )
 ;
