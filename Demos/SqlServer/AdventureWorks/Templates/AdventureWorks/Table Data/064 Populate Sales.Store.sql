
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.Store.tabledata}}';



MERGE INTO [Sales].[Store] AS Target
USING (
  SELECT [BusinessEntityID],[Demographics],[ModifiedDate],[Name],[SalesPersonID]
    FROM OPENJSON(@v_json)
    WITH (
           [BusinessEntityID] INT,
           [Demographics] XML([Sales].[StoreSurveySchemaCollection]),
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [rowguid] UNIQUEIDENTIFIER,
           [SalesPersonID] INT
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (CAST(Target.[Demographics] AS NVARCHAR(MAX)) = CAST(Source.[Demographics] AS NVARCHAR(MAX)) OR (Target.[Demographics] IS NULL AND Source.[Demographics] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[SalesPersonID] = Source.[SalesPersonID] OR (Target.[SalesPersonID] IS NULL AND Source.[SalesPersonID] IS NULL))) THEN
  UPDATE SET
        [BusinessEntityID] = Source.[BusinessEntityID],
        [Demographics] = Source.[Demographics],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [SalesPersonID] = Source.[SalesPersonID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BusinessEntityID],
        [Demographics],
        [ModifiedDate],
        [Name],
        [SalesPersonID]
   ) VALUES (
         Source.[BusinessEntityID],
        Source.[Demographics],
        Source.[ModifiedDate],
        Source.[Name],
        Source.[SalesPersonID]
   )
 ;
