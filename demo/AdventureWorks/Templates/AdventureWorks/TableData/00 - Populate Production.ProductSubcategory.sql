DECLARE @data VARCHAR(MAX) = '[
    {
        "ProductSubcategoryID": 1,
        "ProductCategoryID": 1,
        "Name": "Mountain Bikes",
        "rowguid": "2D364ADE-264A-433C-B092-4FCBF3804E01",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 2,
        "ProductCategoryID": 1,
        "Name": "Road Bikes",
        "rowguid": "000310C0-BCC8-42C4-B0C3-45AE611AF06B",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 3,
        "ProductCategoryID": 1,
        "Name": "Touring Bikes",
        "rowguid": "02C5061D-ECDC-4274-B5F1-E91D76BC3F37",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 4,
        "ProductCategoryID": 2,
        "Name": "Handlebars",
        "rowguid": "3EF2C725-7135-4C85-9AE6-AE9A3BDD9283",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 5,
        "ProductCategoryID": 2,
        "Name": "Bottom Brackets",
        "rowguid": "A9E54089-8A1E-4CF5-8646-E3801F685934",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 6,
        "ProductCategoryID": 2,
        "Name": "Brakes",
        "rowguid": "D43BA4A3-EF0D-426B-90EB-4BE4547DD30C",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 7,
        "ProductCategoryID": 2,
        "Name": "Chains",
        "rowguid": "E93A7231-F16C-4B0F-8C41-C73FDEC62DA0",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 8,
        "ProductCategoryID": 2,
        "Name": "Cranksets",
        "rowguid": "4F644521-422B-4F19-974A-E3DF6102567E",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 9,
        "ProductCategoryID": 2,
        "Name": "Derailleurs",
        "rowguid": "1830D70C-AA2A-40C0-A271-5BA86F38F8BF",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 10,
        "ProductCategoryID": 2,
        "Name": "Forks",
        "rowguid": "B5F9BA42-B69B-4FDD-B2EC-57FB7B42E3CF",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 11,
        "ProductCategoryID": 2,
        "Name": "Headsets",
        "rowguid": "7C782BBE-5A16-495A-AA50-10AFE5A84AF2",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 12,
        "ProductCategoryID": 2,
        "Name": "Mountain Frames",
        "rowguid": "61B21B65-E16A-4BE7-9300-4D8E9DB861BE",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 13,
        "ProductCategoryID": 2,
        "Name": "Pedals",
        "rowguid": "6D24AC07-7A84-4849-864A-865A14125BC9",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 14,
        "ProductCategoryID": 2,
        "Name": "Road Frames",
        "rowguid": "5515F857-075B-4F9A-87B7-43B4997077B3",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 15,
        "ProductCategoryID": 2,
        "Name": "Saddles",
        "rowguid": "049FFFA3-9D30-46DF-82F7-F20730EC02B3",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 16,
        "ProductCategoryID": 2,
        "Name": "Touring Frames",
        "rowguid": "D2E3F1A8-56C4-4F36-B29D-5659FC0D2789",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 17,
        "ProductCategoryID": 2,
        "Name": "Wheels",
        "rowguid": "43521287-4B0B-438E-B80E-D82D9AD7C9F0",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 18,
        "ProductCategoryID": 3,
        "Name": "Bib-Shorts",
        "rowguid": "67B58D2B-5798-4A90-8C6C-5DDACF057171",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 19,
        "ProductCategoryID": 3,
        "Name": "Caps",
        "rowguid": "430DD6A8-A755-4B23-BB05-52520107DA5F",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 20,
        "ProductCategoryID": 3,
        "Name": "Gloves",
        "rowguid": "92D5657B-0032-4E49-BAD5-41A441A70942",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 21,
        "ProductCategoryID": 3,
        "Name": "Jerseys",
        "rowguid": "09E91437-BA4F-4B1A-8215-74184FD95DB8",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 22,
        "ProductCategoryID": 3,
        "Name": "Shorts",
        "rowguid": "1A5BA5B3-03C3-457C-B11E-4FA85EDE87DA",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 23,
        "ProductCategoryID": 3,
        "Name": "Socks",
        "rowguid": "701019C3-09FE-4949-8386-C6CE686474E5",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 24,
        "ProductCategoryID": 3,
        "Name": "Tights",
        "rowguid": "5DEB3E55-9897-4416-B18A-515E970BC2D1",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 25,
        "ProductCategoryID": 3,
        "Name": "Vests",
        "rowguid": "9AD7FE93-5BA0-4736-B578-FF80A2071297",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 26,
        "ProductCategoryID": 4,
        "Name": "Bike Racks",
        "rowguid": "4624B5CE-66D6-496B-9201-C053DF3556CC",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 27,
        "ProductCategoryID": 4,
        "Name": "Bike Stands",
        "rowguid": "43B445C8-B820-424E-A1D5-90D81DA0B46F",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 28,
        "ProductCategoryID": 4,
        "Name": "Bottles and Cages",
        "rowguid": "9B7DFF41-9FA3-4776-8DEF-2C9A48C8B779",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 29,
        "ProductCategoryID": 4,
        "Name": "Cleaners",
        "rowguid": "9AD3BCF0-244D-4EC4-A6A0-FB701351C6A3",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 30,
        "ProductCategoryID": 4,
        "Name": "Fenders",
        "rowguid": "1697F8A2-0A08-4883-B7DD-D19117B4E9A7",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 31,
        "ProductCategoryID": 4,
        "Name": "Helmets",
        "rowguid": "F5E07A33-C9E0-439C-B5F3-9F25FB65BECC",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 32,
        "ProductCategoryID": 4,
        "Name": "Hydration Packs",
        "rowguid": "646A8906-FC87-4267-A443-9C6D791E6693",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 33,
        "ProductCategoryID": 4,
        "Name": "Lights",
        "rowguid": "954178BA-624F-42DB-95F6-CA035F36D130",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 34,
        "ProductCategoryID": 4,
        "Name": "Locks",
        "rowguid": "19646983-3FA0-4773-9A0C-F34C49DF9BC8",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 35,
        "ProductCategoryID": 4,
        "Name": "Panniers",
        "rowguid": "3002A5D5-FEC3-464B-BEF3-E0F81D35F431",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 36,
        "ProductCategoryID": 4,
        "Name": "Pumps",
        "rowguid": "FE4D46F2-C87C-48C5-A4A1-3F55712D80B1",
        "ModifiedDate": "2008-04-30T00:00:00"
    },
    {
        "ProductSubcategoryID": 37,
        "ProductCategoryID": 4,
        "Name": "Tires and Tubes",
        "rowguid": "3C17C9AE-E906-48B4-BDD3-60E28D47DCDF",
        "ModifiedDate": "2008-04-30T00:00:00"
    }
]'

MERGE INTO Production.ProductSubcategory AS Target
USING (
    SELECT *
    FROM OPENJSON(@data)
    WITH (
        ProductSubcategoryID int,
        ProductCategoryID int,
        Name nvarchar(50),
        rowguid uniqueidentifier,
        ModifiedDate datetime
    )
) AS Source
ON Target.ProductSubcategoryID = Source.ProductSubcategoryID
WHEN MATCHED THEN
    UPDATE SET
        ProductCategoryID = Source.ProductCategoryID,
        Name = Source.Name,
        rowguid = Source.rowguid,
        ModifiedDate = Source.ModifiedDate
WHEN NOT MATCHED THEN
    INSERT (ProductCategoryID, Name, rowguid, ModifiedDate)
    VALUES (Source.ProductCategoryID, Source.Name, Source.rowguid, Source.ModifiedDate);
    