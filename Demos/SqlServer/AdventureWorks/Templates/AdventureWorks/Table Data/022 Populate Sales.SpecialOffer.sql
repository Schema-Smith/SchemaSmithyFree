
DECLARE @v_json NVARCHAR(MAX) = '{{Sales.SpecialOffer.tabledata}}';


SET IDENTITY_INSERT [Sales].[SpecialOffer] ON;
MERGE INTO [Sales].[SpecialOffer] AS Target
USING (
  SELECT [Category],[Description],[DiscountPct],[EndDate],[MaxQty],[MinQty],[ModifiedDate],[SpecialOfferID],[StartDate],[Type]
    FROM OPENJSON(@v_json)
    WITH (
           [Category] NVARCHAR(50),
           [Description] NVARCHAR(255),
           [DiscountPct] SMALLMONEY,
           [EndDate] DATETIME,
           [MaxQty] INT,
           [MinQty] INT,
           [ModifiedDate] DATETIME,
           [rowguid] UNIQUEIDENTIFIER,
           [SpecialOfferID] INT,
           [StartDate] DATETIME,
           [Type] NVARCHAR(50)
    )
) AS Source
ON Source.[SpecialOfferID] = Target.[SpecialOfferID]

WHEN MATCHED AND (NOT (Target.[Category] = Source.[Category] OR (Target.[Category] IS NULL AND Source.[Category] IS NULL)) OR NOT (Target.[Description] = Source.[Description] OR (Target.[Description] IS NULL AND Source.[Description] IS NULL)) OR NOT (Target.[DiscountPct] = Source.[DiscountPct] OR (Target.[DiscountPct] IS NULL AND Source.[DiscountPct] IS NULL)) OR NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) OR NOT (Target.[MaxQty] = Source.[MaxQty] OR (Target.[MaxQty] IS NULL AND Source.[MaxQty] IS NULL)) OR NOT (Target.[MinQty] = Source.[MinQty] OR (Target.[MinQty] IS NULL AND Source.[MinQty] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL)) OR NOT (Target.[Type] = Source.[Type] OR (Target.[Type] IS NULL AND Source.[Type] IS NULL))) THEN
  UPDATE SET
        [Category] = Source.[Category],
        [Description] = Source.[Description],
        [DiscountPct] = Source.[DiscountPct],
        [EndDate] = Source.[EndDate],
        [MaxQty] = Source.[MaxQty],
        [MinQty] = Source.[MinQty],
        [ModifiedDate] = Source.[ModifiedDate],
        [StartDate] = Source.[StartDate],
        [Type] = Source.[Type]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [Category],
        [Description],
        [DiscountPct],
        [EndDate],
        [MaxQty],
        [MinQty],
        [ModifiedDate],
        [SpecialOfferID],
        [StartDate],
        [Type]
   ) VALUES (
         Source.[Category],
        Source.[Description],
        Source.[DiscountPct],
        Source.[EndDate],
        Source.[MaxQty],
        Source.[MinQty],
        Source.[ModifiedDate],
        Source.[SpecialOfferID],
        Source.[StartDate],
        Source.[Type]
   )
 ;
SET IDENTITY_INSERT [Sales].[SpecialOffer] OFF;
