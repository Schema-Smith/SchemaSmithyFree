
DECLARE @v_json NVARCHAR(MAX) = '{{Person.CountryRegion.tabledata}}';



MERGE INTO [Person].[CountryRegion] AS Target
USING (
  SELECT [CountryRegionCode],[ModifiedDate],[Name]
    FROM OPENJSON(@v_json)
    WITH (
           [CountryRegionCode] NVARCHAR(3),
           [ModifiedDate] DATETIME,
           [Name] NAME
    )
) AS Source
ON Source.[CountryRegionCode] = Target.[CountryRegionCode]

WHEN MATCHED AND (NOT (Target.[CountryRegionCode] = Source.[CountryRegionCode] OR (Target.[CountryRegionCode] IS NULL AND Source.[CountryRegionCode] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL))) THEN
  UPDATE SET
        [CountryRegionCode] = Source.[CountryRegionCode],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CountryRegionCode],
        [ModifiedDate],
        [Name]
   ) VALUES (
         Source.[CountryRegionCode],
        Source.[ModifiedDate],
        Source.[Name]
   )
 ;
