DECLARE @data VARCHAR(MAX) = '[
    {
        "AddressTypeID": 1,
        "Name": "Billing",
        "rowguid": "B84F78B1-4EFE-4A0E-8CB7-70E9F112F886",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "AddressTypeID": 2,
        "Name": "Home",
        "rowguid": "41BC2FF6-F0FC-475F-8EB9-CEC0805AA0F2",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "AddressTypeID": 3,
        "Name": "Main Office",
        "rowguid": "8EEEC28C-07A2-4FB9-AD0A-42D4A0BBC575",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "AddressTypeID": 4,
        "Name": "Primary",
        "rowguid": "24CB3088-4345-47C4-86C5-17B535133D1E",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "AddressTypeID": 5,
        "Name": "Shipping",
        "rowguid": "B29DA3F8-19A3-47DA-9DAA-15C84F4A83A5",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "AddressTypeID": 6,
        "Name": "Archive",
        "rowguid": "A67F238A-5BA2-444B-966C-0467ED9C427F",
        "ModifiedDate": "2008-04-30T00:00:00"
    }
]'

MERGE INTO Person.AddressType AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        AddressTypeID int,
        Name nvarchar(50),
        rowguid uniqueidentifier,
        ModifiedDate datetime
    )
) AS Source
ON Target.AddressTypeID = Source.AddressTypeID
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        rowguid = Source.rowguid,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (Name, rowguid, ModifiedDate)
    VALUES (Source.Name, Source.rowguid, Source.ModifiedDate);
