
DECLARE @v_json NVARCHAR(MAX) = '[
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Boxes","UnitMeasureCode":"BOX"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Bottle","UnitMeasureCode":"BTL"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Celsius","UnitMeasureCode":"C  "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Canister","UnitMeasureCode":"CAN"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Carton","UnitMeasureCode":"CAR"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Cubic meters","UnitMeasureCode":"CBM"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Cubic centimeter","UnitMeasureCode":"CCM"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Cubic decimeter","UnitMeasureCode":"CDM"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Centimeter","UnitMeasureCode":"CM "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Square centimeter","UnitMeasureCode":"CM2"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Crate","UnitMeasureCode":"CR "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Case","UnitMeasureCode":"CS "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Container","UnitMeasureCode":"CTN"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Decimeter","UnitMeasureCode":"DM "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Dozen","UnitMeasureCode":"DZ "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Each","UnitMeasureCode":"EA "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Cubic foot","UnitMeasureCode":"FT3"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Gram","UnitMeasureCode":"G  "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Gallon","UnitMeasureCode":"GAL"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Inch","UnitMeasureCode":"IN "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Kilogram","UnitMeasureCode":"KG "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Kilogram\/cubic meter","UnitMeasureCode":"KGV"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Kilometer","UnitMeasureCode":"KM "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Kiloton","UnitMeasureCode":"KT "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Liter","UnitMeasureCode":"L  "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"US pound","UnitMeasureCode":"LB "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Meter","UnitMeasureCode":"M  "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Square meter","UnitMeasureCode":"M2 "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Cubic meter","UnitMeasureCode":"M3 "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Milligram","UnitMeasureCode":"MG "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Milliliter","UnitMeasureCode":"ML "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Millimeter","UnitMeasureCode":"MM "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Ounces","UnitMeasureCode":"OZ "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Pack","UnitMeasureCode":"PAK"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Pallet","UnitMeasureCode":"PAL"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Piece","UnitMeasureCode":"PC "},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Percentage","UnitMeasureCode":"PCT"},
{"ModifiedDate":"2008-04-30T00:00:00","Name":"Pint, US liquid","UnitMeasureCode":"PT "}
]';

ALTER TABLE [Production].[UnitMeasure] DISABLE TRIGGER ALL;
 
MERGE INTO [Production].[UnitMeasure] AS Target
USING (
  SELECT [ModifiedDate],[Name],[UnitMeasureCode]
    FROM OPENJSON(@v_json)
    WITH (
           [ModifiedDate] DATETIME,
           [Name] NAME,
           [UnitMeasureCode] NCHAR(3)
    )
) AS Source
ON Source.[UnitMeasureCode] = Target.[UnitMeasureCode]


WHEN MATCHED AND (NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) AND NOT (Target.[Name] = Source.[Name] OR (Target.[Name] IS NULL AND Source.[Name] IS NULL)) AND NOT (Target.[UnitMeasureCode] = Source.[UnitMeasureCode] OR (Target.[UnitMeasureCode] IS NULL AND Source.[UnitMeasureCode] IS NULL))) THEN
  UPDATE SET
        [ModifiedDate] = Source.[ModifiedDate],
        [Name] = Source.[Name],
        [UnitMeasureCode] = Source.[UnitMeasureCode]


WHEN NOT MATCHED THEN
  INSERT (
        [ModifiedDate],
        [Name],
        [UnitMeasureCode]
  ) VALUES (
        Source.[ModifiedDate],
        Source.[Name],
        Source.[UnitMeasureCode]  
  )
;
ALTER TABLE [Production].[UnitMeasure] ENABLE TRIGGER ALL;
