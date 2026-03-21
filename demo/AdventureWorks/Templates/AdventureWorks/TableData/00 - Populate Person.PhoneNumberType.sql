DECLARE @data VARCHAR(MAX) = '[
    {
        "PhoneNumberTypeID": 1,
        "Name": "Cell",
        "ModifiedDate": "2017-12-13T13:19:22.273"
    },
    {
        "PhoneNumberTypeID": 2,
        "Name": "Home",
        "ModifiedDate": "2017-12-13T13:19:22.273"
    },
    {
        "PhoneNumberTypeID": 3,
        "Name": "Work",
        "ModifiedDate": "2017-12-13T13:19:22.273"
    }
]'

MERGE INTO Person.PhoneNumberType AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        PhoneNumberTypeID int,
        Name nvarchar(50),
        ModifiedDate datetime
    )
) AS Source
ON Target.PhoneNumberTypeID = Source.PhoneNumberTypeID
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (Name, ModifiedDate)
    VALUES (Source.Name, Source.ModifiedDate);