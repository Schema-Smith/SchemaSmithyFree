DECLARE @data VARCHAR(MAX) = '[
    {
        "SalesReasonID": 1,
        "Name": "Price",
        "ReasonType": "Other",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 2,
        "Name": "On Promotion",
        "ReasonType": "Promotion",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 3,
        "Name": "Magazine Advertisement",
        "ReasonType": "Marketing",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 4,
        "Name": "Television  Advertisement",
        "ReasonType": "Marketing",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 5,
        "Name": "Manufacturer",
        "ReasonType": "Other",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 6,
        "Name": "Review",
        "ReasonType": "Other",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 7,
        "Name": "Demo Event",
        "ReasonType": "Marketing",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 8,
        "Name": "Sponsorship",
        "ReasonType": "Marketing",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 9,
        "Name": "Quality",
        "ReasonType": "Other",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "SalesReasonID": 10,
        "Name": "Other",
        "ReasonType": "Other",
        "ModifiedDate": "2008-04-30T00:00:00"
    }
]'

MERGE INTO Sales.SalesReason AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        SalesReasonID int,
        Name nvarchar(50),
        ReasonType nvarchar(50),
        ModifiedDate datetime
    )
) AS Source
ON Target.SalesReasonID = Source.SalesReasonID
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        ReasonType = Source.ReasonType,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (Name, ReasonType, ModifiedDate)
    VALUES (Source.Name, Source.ReasonType, Source.ModifiedDate);
    