
DECLARE @v_json NVARCHAR(MAX) = '{{dbo.Invoice.tabledata}}';


SET IDENTITY_INSERT [dbo].[Invoice] ON;
MERGE INTO [dbo].[Invoice] AS Target
USING (
  SELECT [BillingAddress],[BillingCity],[BillingCountry],[BillingPostalCode],[BillingState],[CustomerId],[InvoiceDate],[InvoiceId],[Total]
    FROM OPENJSON(@v_json)
    WITH (
           [BillingAddress] NVARCHAR(70),
           [BillingCity] NVARCHAR(40),
           [BillingCountry] NVARCHAR(40),
           [BillingPostalCode] NVARCHAR(10),
           [BillingState] NVARCHAR(40),
           [CustomerId] INT,
           [InvoiceDate] DATETIME,
           [InvoiceId] INT,
           [Total] NUMERIC(10, 2)
    )
) AS Source
ON Source.[InvoiceId] = Target.[InvoiceId]

WHEN MATCHED AND (NOT (Target.[BillingAddress] = Source.[BillingAddress] OR (Target.[BillingAddress] IS NULL AND Source.[BillingAddress] IS NULL)) OR NOT (Target.[BillingCity] = Source.[BillingCity] OR (Target.[BillingCity] IS NULL AND Source.[BillingCity] IS NULL)) OR NOT (Target.[BillingCountry] = Source.[BillingCountry] OR (Target.[BillingCountry] IS NULL AND Source.[BillingCountry] IS NULL)) OR NOT (Target.[BillingPostalCode] = Source.[BillingPostalCode] OR (Target.[BillingPostalCode] IS NULL AND Source.[BillingPostalCode] IS NULL)) OR NOT (Target.[BillingState] = Source.[BillingState] OR (Target.[BillingState] IS NULL AND Source.[BillingState] IS NULL)) OR NOT (Target.[CustomerId] = Source.[CustomerId] OR (Target.[CustomerId] IS NULL AND Source.[CustomerId] IS NULL)) OR NOT (Target.[InvoiceDate] = Source.[InvoiceDate] OR (Target.[InvoiceDate] IS NULL AND Source.[InvoiceDate] IS NULL)) OR NOT (Target.[Total] = Source.[Total] OR (Target.[Total] IS NULL AND Source.[Total] IS NULL))) THEN
  UPDATE SET
        [BillingAddress] = Source.[BillingAddress],
        [BillingCity] = Source.[BillingCity],
        [BillingCountry] = Source.[BillingCountry],
        [BillingPostalCode] = Source.[BillingPostalCode],
        [BillingState] = Source.[BillingState],
        [CustomerId] = Source.[CustomerId],
        [InvoiceDate] = Source.[InvoiceDate],
        [Total] = Source.[Total]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BillingAddress],
        [BillingCity],
        [BillingCountry],
        [BillingPostalCode],
        [BillingState],
        [CustomerId],
        [InvoiceDate],
        [InvoiceId],
        [Total]
   ) VALUES (
         Source.[BillingAddress],
        Source.[BillingCity],
        Source.[BillingCountry],
        Source.[BillingPostalCode],
        Source.[BillingState],
        Source.[CustomerId],
        Source.[InvoiceDate],
        Source.[InvoiceId],
        Source.[Total]
   )
 ;
SET IDENTITY_INSERT [dbo].[Invoice] OFF;
