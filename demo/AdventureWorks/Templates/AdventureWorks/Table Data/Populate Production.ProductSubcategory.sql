
DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Mountain Bikes","ProductCategoryID":1,"ProductSubcategoryID":1},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Road Bikes","ProductCategoryID":1,"ProductSubcategoryID":2},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Touring Bikes","ProductCategoryID":1,"ProductSubcategoryID":3},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Handlebars","ProductCategoryID":2,"ProductSubcategoryID":4},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Bottom Brackets","ProductCategoryID":2,"ProductSubcategoryID":5},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Brakes","ProductCategoryID":2,"ProductSubcategoryID":6},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Chains","ProductCategoryID":2,"ProductSubcategoryID":7},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Cranksets","ProductCategoryID":2,"ProductSubcategoryID":8},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Derailleurs","ProductCategoryID":2,"ProductSubcategoryID":9},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Forks","ProductCategoryID":2,"ProductSubcategoryID":10},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Headsets","ProductCategoryID":2,"ProductSubcategoryID":11},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Mountain Frames","ProductCategoryID":2,"ProductSubcategoryID":12},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Pedals","ProductCategoryID":2,"ProductSubcategoryID":13},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Road Frames","ProductCategoryID":2,"ProductSubcategoryID":14},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Saddles","ProductCategoryID":2,"ProductSubcategoryID":15},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Touring Frames","ProductCategoryID":2,"ProductSubcategoryID":16},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Wheels","ProductCategoryID":2,"ProductSubcategoryID":17},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Bib-Shorts","ProductCategoryID":3,"ProductSubcategoryID":18},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Caps","ProductCategoryID":3,"ProductSubcategoryID":19},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Gloves","ProductCategoryID":3,"ProductSubcategoryID":20},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Jerseys","ProductCategoryID":3,"ProductSubcategoryID":21},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Shorts","ProductCategoryID":3,"ProductSubcategoryID":22},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Socks","ProductCategoryID":3,"ProductSubcategoryID":23},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Tights","ProductCategoryID":3,"ProductSubcategoryID":24},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Vests","ProductCategoryID":3,"ProductSubcategoryID":25},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Bike Racks","ProductCategoryID":4,"ProductSubcategoryID":26},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Bike Stands","ProductCategoryID":4,"ProductSubcategoryID":27},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Bottles and Cages","ProductCategoryID":4,"ProductSubcategoryID":28},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Cleaners","ProductCategoryID":4,"ProductSubcategoryID":29},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Fenders","ProductCategoryID":4,"ProductSubcategoryID":30},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Helmets","ProductCategoryID":4,"ProductSubcategoryID":31},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Hydration Packs","ProductCategoryID":4,"ProductSubcategoryID":32},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Lights","ProductCategoryID":4,"ProductSubcategoryID":33},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Locks","ProductCategoryID":4,"ProductSubcategoryID":34},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Panniers","ProductCategoryID":4,"ProductSubcategoryID":35},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Pumps","ProductCategoryID":4,"ProductSubcategoryID":36},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Tires and Tubes","ProductCategoryID":4,"ProductSubcategoryID":37}
]';

ALTER TABLE [Production].[ProductSubcategory] DISABLE TRIGGER ALL;
SET IDENTITY_INSERT [Production].[ProductSubcategory] ON; 
MERGE INTO [Production].[ProductSubcategory] AS Target
USING (
  SELECT [ModifiedDate],[Name],[ProductCategoryID],[ProductSubcategoryID]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [ProductCategoryID] INT,
           [ProductSubcategoryID] INT,
           [rowguid] UNIQUEIDENTIFIER
    )
) AS Source
ON Source.[ProductSubcategoryID] = Target.[ProductSubcategoryID]


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) AND NOT (Target.[ProductCategoryID] = Source.[ProductCategoryID] OR (Target.[ProductCategoryID] IS NULL AND Source.[ProductCategoryID] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [ProductCategoryID] = Source.[ProductCategoryID]


WHEN NOT MATCHED THEN
  INSERT (
        [ModifiedDate],
        [Name],
        [ProductCategoryID],
        [ProductSubcategoryID]
  ) VALUES (
        Source.[ModifiedDate],
        Source.[Name],
        Source.[ProductCategoryID],
        Source.[ProductSubcategoryID]  
  )
;
SET IDENTITY_INSERT [Production].[ProductSubcategory] OFF;
ALTER TABLE [Production].[ProductSubcategory] ENABLE TRIGGER ALL;
