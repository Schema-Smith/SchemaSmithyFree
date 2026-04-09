
DECLARE @v_json NVARCHAR(MAX) = '{{Purchasing.Vendor.tabledata}}';



MERGE INTO [Purchasing].[Vendor] AS Target
USING (
  SELECT [AccountNumber],[ActiveFlag],[BusinessEntityID],[CreditRating],[ModifiedDate],[Name],[PreferredVendorStatus],[PurchasingWebServiceURL]
    FROM OPENJSON(@v_json)
    WITH (
           [AccountNumber] ACCOUNTNUMBER,
           [ActiveFlag] FLAG,
           [BusinessEntityID] INT,
           [CreditRating] TINYINT,
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [PreferredVendorStatus] FLAG,
           [PurchasingWebServiceURL] NVARCHAR(1024)
    )
) AS Source
ON Source.[BusinessEntityID] = Target.[BusinessEntityID]

WHEN MATCHED AND (NOT (Target.[AccountNumber] = Source.[AccountNumber] OR (Target.[AccountNumber] IS NULL AND Source.[AccountNumber] IS NULL)) OR NOT (Target.[ActiveFlag] = Source.[ActiveFlag] OR (Target.[ActiveFlag] IS NULL AND Source.[ActiveFlag] IS NULL)) OR NOT (Target.[BusinessEntityID] = Source.[BusinessEntityID] OR (Target.[BusinessEntityID] IS NULL AND Source.[BusinessEntityID] IS NULL)) OR NOT (Target.[CreditRating] = Source.[CreditRating] OR (Target.[CreditRating] IS NULL AND Source.[CreditRating] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) OR NOT (Target.[PreferredVendorStatus] = Source.[PreferredVendorStatus] OR (Target.[PreferredVendorStatus] IS NULL AND Source.[PreferredVendorStatus] IS NULL)) OR NOT (Target.[PurchasingWebServiceURL] = Source.[PurchasingWebServiceURL] OR (Target.[PurchasingWebServiceURL] IS NULL AND Source.[PurchasingWebServiceURL] IS NULL))) THEN
  UPDATE SET
        [AccountNumber] = Source.[AccountNumber],
        [ActiveFlag] = Source.[ActiveFlag],
        [BusinessEntityID] = Source.[BusinessEntityID],
        [CreditRating] = Source.[CreditRating],
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [PreferredVendorStatus] = Source.[PreferredVendorStatus],
        [PurchasingWebServiceURL] = Source.[PurchasingWebServiceURL]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [AccountNumber],
        [ActiveFlag],
        [BusinessEntityID],
        [CreditRating],
        [ModifiedDate],
        [Name],
        [PreferredVendorStatus],
        [PurchasingWebServiceURL]
   ) VALUES (
         Source.[AccountNumber],
        Source.[ActiveFlag],
        Source.[BusinessEntityID],
        Source.[CreditRating],
        Source.[ModifiedDate],
        Source.[Name],
        Source.[PreferredVendorStatus],
        Source.[PurchasingWebServiceURL]
   )
 ;
