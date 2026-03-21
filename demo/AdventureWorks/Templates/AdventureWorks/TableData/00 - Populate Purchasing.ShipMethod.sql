DECLARE @data VARCHAR(MAX) = '[
    {
        "ShipMethodID": 1,
        "Name": "XRQ - TRUCK GROUND",
        "ShipBase": 3.9500,
        "ShipRate": 0.9900,
        "rowguid": "6BE756D9-D7BE-4463-8F2C-AE60C710D606",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ShipMethodID": 2,
        "Name": "ZY - EXPRESS",
        "ShipBase": 9.9500,
        "ShipRate": 1.9900,
        "rowguid": "3455079B-F773-4DC6-8F1E-2A58649C4AB8",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ShipMethodID": 3,
        "Name": "OVERSEAS - DELUXE",
        "ShipBase": 29.9500,
        "ShipRate": 2.9900,
        "rowguid": "22F4E461-28CF-4ACE-A980-F686CF112EC8",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ShipMethodID": 4,
        "Name": "OVERNIGHT J-FAST",
        "ShipBase": 21.9500,
        "ShipRate": 1.2900,
        "rowguid": "107E8356-E7A8-463D-B60C-079FFF467F3F",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ShipMethodID": 5,
        "Name": "CARGO TRANSPORT 5",
        "ShipBase": 8.9900,
        "ShipRate": 1.4900,
        "rowguid": "B166019A-B134-4E76-B957-2B0490C610ED",
        "ModifiedDate": "2008-04-30T00:00:00"
    }
]'

MERGE INTO Purchasing.ShipMethod AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        ShipMethodID int,
        Name nvarchar(50),
        ShipBase money,
        ShipRate money,
        rowguid uniqueidentifier,
        ModifiedDate datetime
    )
) AS Source
ON Target.ShipMethodID = Source.ShipMethodID
WHEN MATCHED THEN
    UPDATE SET
        Name = Source.Name,
        ShipBase = Source.ShipBase,
        ShipRate = Source.ShipRate,
        rowguid = Source.rowguid,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (Name, ShipBase, ShipRate, rowguid, ModifiedDate)
    VALUES (Source.Name, Source.ShipBase, Source.ShipRate, Source.rowguid, Source.ModifiedDate);
    