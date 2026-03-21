
DECLARE @v_json NVARCHAR(MAX) = '[
{"CompanyName":"Speedy Express","Phone":"(503) 555-9831","ShipperID":1},
{"CompanyName":"United Package","Phone":"(503) 555-3199","ShipperID":2},
{"CompanyName":"Federal Shipping","Phone":"(503) 555-9931","ShipperID":3}
]';

ALTER TABLE [dbo].[Shippers] DISABLE TRIGGER ALL;
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


WHEN MATCHED AND (NOT (Target.[CompanyName] = Source.[CompanyName] OR (Target.[CompanyName] IS NULL AND Source.[CompanyName] IS NULL)) AND NOT (Target.[Phone] = Source.[Phone] OR (Target.[Phone] IS NULL AND Source.[Phone] IS NULL))) THEN
  UPDATE SET
        [CompanyName] = Source.[CompanyName],
        [Phone] = Source.[Phone]


WHEN NOT MATCHED THEN
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
ALTER TABLE [dbo].[Shippers] ENABLE TRIGGER ALL;
