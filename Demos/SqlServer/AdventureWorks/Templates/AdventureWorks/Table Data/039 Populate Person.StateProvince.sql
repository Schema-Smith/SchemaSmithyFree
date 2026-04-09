
DECLARE @v_json NVARCHAR(MAX) = '{{Person.StateProvince.tabledata}}';


SET IDENTITY_INSERT [Person].[StateProvince] ON;
MERGE INTO [Person].[StateProvince] AS Target
USING (
  SELECT [CountryRegionCode],[IsOnlyStateProvinceFlag],[ModifiedDate],[Name],[StateProvinceCode],[StateProvinceID],[TerritoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [CountryRegionCode] NVARCHAR(3),
           [IsOnlyStateProvinceFlag] FLAG,
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [rowguid] UNIQUEIDENTIFIER,
           [StateProvinceCode] NCHAR(3),
           [StateProvinceID] INT,
           [TerritoryID] INT
    )
) AS Source
ON Source.[StateProvinceID] = Target.[StateProvinceID]

WHEN MATCHED AND (NOT (Target.[CountryRegionCode] = Source.[CountryRegionCode] OR (Target.[CountryRegionCode] IS NULL AND Source.[CountryRegionCode] IS NULL)) OR NOT (Target.[IsOnlyStateProvinceFlag] = Source.[IsOnlyStateProvinceFlag] OR (Target.[IsOnlyStateProvinceFlag] IS NULL AND Source.[IsOnlyStateProvinceFlag] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[StateProvinceCode] = Source.[StateProvinceCode] OR (Target.[StateProvinceCode] IS NULL AND Source.[StateProvinceCode] IS NULL)) OR NOT (Target.[TerritoryID] = Source.[TerritoryID] OR (Target.[TerritoryID] IS NULL AND Source.[TerritoryID] IS NULL))) THEN
  UPDATE SET
        [CountryRegionCode] = Source.[CountryRegionCode],
        [IsOnlyStateProvinceFlag] = Source.[IsOnlyStateProvinceFlag],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [StateProvinceCode] = Source.[StateProvinceCode],
        [TerritoryID] = Source.[TerritoryID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CountryRegionCode],
        [IsOnlyStateProvinceFlag],
        [ModifiedDate],
        [Name],
        [StateProvinceCode],
        [StateProvinceID],
        [TerritoryID]
   ) VALUES (
         Source.[CountryRegionCode],
        Source.[IsOnlyStateProvinceFlag],
        Source.[ModifiedDate],
        Source.[Name],
        Source.[StateProvinceCode],
        Source.[StateProvinceID],
        Source.[TerritoryID]
   )
 ;
SET IDENTITY_INSERT [Person].[StateProvince] OFF;
