
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.InvoiceLine.tabledata}}';


SET IDENTITY_INSERT [dbo].[InvoiceLine] ON;
MERGE INTO [dbo].[InvoiceLine] AS Target
USING (
  SELECT [InvoiceId],[InvoiceLineId],[Quantity],[TrackId],[UnitPrice]
    FROM OPENJSON(@v_json)
    WITH (
           [InvoiceId] INT,
           [InvoiceLineId] INT,
           [Quantity] INT,
           [TrackId] INT,
           [UnitPrice] NUMERIC(10, 2)
    )
) AS Source
ON Source.[InvoiceLineId] = Target.[InvoiceLineId]

WHEN MATCHED AND (NOT (Target.[InvoiceId] = Source.[InvoiceId] OR (Target.[InvoiceId] IS NULL AND Source.[InvoiceId] IS NULL)) OR NOT (Target.[Quantity] = Source.[Quantity] OR (Target.[Quantity] IS NULL AND Source.[Quantity] IS NULL)) OR NOT (Target.[TrackId] = Source.[TrackId] OR (Target.[TrackId] IS NULL AND Source.[TrackId] IS NULL)) OR NOT (Target.[UnitPrice] = Source.[UnitPrice] OR (Target.[UnitPrice] IS NULL AND Source.[UnitPrice] IS NULL))) THEN
  UPDATE SET
        [InvoiceId] = Source.[InvoiceId],
        [Quantity] = Source.[Quantity],
        [TrackId] = Source.[TrackId],
        [UnitPrice] = Source.[UnitPrice]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [InvoiceId],
        [InvoiceLineId],
        [Quantity],
        [TrackId],
        [UnitPrice]
   ) VALUES (
         Source.[InvoiceId],
        Source.[InvoiceLineId],
        Source.[Quantity],
        Source.[TrackId],
        Source.[UnitPrice]
   )
 ;
SET IDENTITY_INSERT [dbo].[InvoiceLine] OFF;
