DECLARE @data VARCHAR(MAX) = '[
    {
        "CultureID": "      ",
        "Name": "Invariant Language (Invariant Country)",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CultureID": "ar    ",
        "Name": "Arabic",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CultureID": "en    ",
        "Name": "English",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CultureID": "es    ",
        "Name": "Spanish",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CultureID": "fr    ",
        "Name": "French",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CultureID": "he    ",
        "Name": "Hebrew",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CultureID": "th    ",
        "Name": "Thai",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "CultureID": "zh-cht",
        "Name": "Chinese",
        "ModifiedDate": "2008-04-30T00:00:00"
    }
]'

MERGE INTO Production.Culture AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        CultureID nchar(6),
        Name nvarchar(50),
        ModifiedDate datetime
    )
) AS Source
ON Target.CultureID = Source.CultureID
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (CultureID, Name, ModifiedDate)
    VALUES (Source.CultureID, Source.Name, Source.ModifiedDate);
    