
DECLARE @v_json NVARCHAR(MAX) = '{{Person.Address.tabledata}}';


SET IDENTITY_INSERT [Person].[Address] ON;
MERGE INTO [Person].[Address] AS Target
USING (
  SELECT [AddressID],[AddressLine1],[AddressLine2],[City],[ModifiedDate],[PostalCode],geography::STGeomFromText([SpatialLocation], [SpatialLocation.STSrid]) AS [SpatialLocation],[StateProvinceID]
    FROM OPENJSON(@v_json)
    WITH (
           [AddressID] INT,
           [AddressLine1] NVARCHAR(60),
           [AddressLine2] NVARCHAR(60),
           [City] NVARCHAR(30),
           [ModifiedDate] DATETIME,
           [PostalCode] NVARCHAR(15),
           [rowguid] UNIQUEIDENTIFIER,
           [SpatialLocation] NVARCHAR(4000), [SpatialLocation.STSrid] INT,
           [StateProvinceID] INT
    )
) AS Source
ON Source.[AddressID] = Target.[AddressID]

WHEN MATCHED AND (NOT (Target.[AddressLine1] = Source.[AddressLine1] OR (Target.[AddressLine1] IS NULL AND Source.[AddressLine1] IS NULL)) OR NOT (Target.[AddressLine2] = Source.[AddressLine2] OR (Target.[AddressLine2] IS NULL AND Source.[AddressLine2] IS NULL)) OR NOT (Target.[City] = Source.[City] OR (Target.[City] IS NULL AND Source.[City] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[PostalCode] = Source.[PostalCode] OR (Target.[PostalCode] IS NULL AND Source.[PostalCode] IS NULL)) OR NOT (Target.[SpatialLocation].ToString() = Source.[SpatialLocation].ToString() OR (Target.[SpatialLocation] IS NULL AND Source.[SpatialLocation] IS NULL)) OR NOT (Target.[StateProvinceID] = Source.[StateProvinceID] OR (Target.[StateProvinceID] IS NULL AND Source.[StateProvinceID] IS NULL))) THEN
  UPDATE SET
        [AddressLine1] = Source.[AddressLine1],
        [AddressLine2] = Source.[AddressLine2],
        [City] = Source.[City],
        [ModifiedDate] = Source.[ModifiedDate],
        [PostalCode] = Source.[PostalCode],
        [SpatialLocation] = Source.[SpatialLocation],
        [StateProvinceID] = Source.[StateProvinceID]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AddressID],
        [AddressLine1],
        [AddressLine2],
        [City],
        [ModifiedDate],
        [PostalCode],
        [SpatialLocation],
        [StateProvinceID]
   ) VALUES (
         Source.[AddressID],
        Source.[AddressLine1],
        Source.[AddressLine2],
        Source.[City],
        Source.[ModifiedDate],
        Source.[PostalCode],
        Source.[SpatialLocation],
        Source.[StateProvinceID]
   )
 ;
SET IDENTITY_INSERT [Person].[Address] OFF;
