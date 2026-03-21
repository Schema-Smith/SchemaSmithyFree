DECLARE @data VARCHAR(MAX) = '[
    {
        "ProductCategoryID": 1,
        "Name": "Bikes",
        "rowguid": "CFBDA25C-DF71-47A7-B81B-64EE161AA37C",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductCategoryID": 2,
        "Name": "Components",
        "rowguid": "C657828D-D808-4ABA-91A3-AF2CE02300E9",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductCategoryID": 3,
        "Name": "Clothing",
        "rowguid": "10A7C342-CA82-48D4-8A38-46A2EB089B74",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductCategoryID": 4,
        "Name": "Accessories",
        "rowguid": "2BE3BE36-D9A2-4EEE-B593-ED895D97C2A6",
        "ModifiedDate": "2008-04-30T00:00:00"
    }
]'

MERGE INTO Production.ProductCategory AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        ProductCategoryID int,
        Name nvarchar(50),
        rowguid uniqueidentifier,
        ModifiedDate datetime
    )
) AS Source
ON Target.ProductCategoryID = Source.ProductCategoryID
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        rowguid = Source.rowguid,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (Name, rowguid, ModifiedDate)
    VALUES (Source.Name, Source.rowguid, Source.ModifiedDate);
    