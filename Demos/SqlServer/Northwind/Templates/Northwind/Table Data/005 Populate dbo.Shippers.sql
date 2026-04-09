
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Shippers.tabledata}}';


SET IDENTITY_INSERT [dbo].[Shippers] ON;
MERGE INTO [dbo].[Shippers] AS Target
USING (
  SELECT [CompanyName],[Phone],[ShipperID]
    FROM OPENJSON(@v_json)
    WITH (
           [CompanyName] NVARCHAR(40),
           [Phone] NVARCHAR(24),
           [ShipperID] INT
    )
) AS Source
ON Source.[ShipperID] = Target.[ShipperID]

WHEN MATCHED AND (NOT (Target.[CompanyName] = Source.[CompanyName] OR (Target.[CompanyName] IS NULL AND Source.[CompanyName] IS NULL)) OR NOT (Target.[Phone] = Source.[Phone] OR (Target.[Phone] IS NULL AND Source.[Phone] IS NULL))) THEN
  UPDATE SET
        [CompanyName] = Source.[CompanyName],
        [Phone] = Source.[Phone]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [CompanyName],
        [Phone],
        [ShipperID]
   ) VALUES (
         Source.[CompanyName],
        Source.[Phone],
        Source.[ShipperID]
   )
 ;
SET IDENTITY_INSERT [dbo].[Shippers] OFF;
