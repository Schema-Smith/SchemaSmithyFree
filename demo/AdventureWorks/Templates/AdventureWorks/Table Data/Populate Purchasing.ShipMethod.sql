
DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2008-04-30T00:00:00","Name":"XRQ - TRUCK GROUND","ShipBase":3.9500,"ShipMethodID":1,"ShipRate":0.9900},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"ZY - EXPRESS","ShipBase":9.9500,"ShipMethodID":2,"ShipRate":1.9900},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"OVERSEAS - DELUXE","ShipBase":29.9500,"ShipMethodID":3,"ShipRate":2.9900},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"OVERNIGHT J-FAST","ShipBase":21.9500,"ShipMethodID":4,"ShipRate":1.2900},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"CARGO TRANSPORT 5","ShipBase":8.9900,"ShipMethodID":5,"ShipRate":1.4900}
]';

ALTER TABLE [Purchasing].[ShipMethod] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [Purchasing].[ShipMethod] ON; 
MERGE INTO [Purchasing].[ShipMethod] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ShipBase],[ShipMethodID],[ShipRate]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [rowguid] UNIQUEIDENTIFIER,
           [ShipBase] MONEY,
           [ShipMethodID] INT,
           [ShipRate] MONEY
    )
) AS Source
ON Source.[ShipMethodID] = Target.[ShipMethodID]


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) AND NOT (Target.[ShipBase] = Source.[ShipBase] OR (Target.[ShipBase] IS NULL AND Source.[ShipBase] IS NULL)) AND NOT (Target.[ShipRate] = Source.[ShipRate] OR (Target.[ShipRate] IS NULL AND Source.[ShipRate] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [ShipBase] = Source.[ShipBase],
        [ShipRate] = Source.[ShipRate]


WHEN NOT MATCHED THEN
  INSERT (
        [ModifiedDate],
        [Name],
        [ShipBase],
        [ShipMethodID],
        [ShipRate]
  ) VALUES (
        Source.[ModifiedDate],
        Source.[Name],
        Source.[ShipBase],
        Source.[ShipMethodID],
        Source.[ShipRate]  
  )
;
SET IDENTITY_INSERT [Purchasing].[ShipMethod] OFF;
ALTER TABLE [Purchasing].[ShipMethod] ENABLE TRIGGER ALL;
