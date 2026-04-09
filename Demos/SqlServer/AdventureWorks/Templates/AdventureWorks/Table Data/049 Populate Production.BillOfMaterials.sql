
DECLARE @v_json NVARCHAR(MAX) = '{{Production.BillOfMaterials.tabledata}}';


SET IDENTITY_INSERT [Production].[BillOfMaterials] ON;
MERGE INTO [Production].[BillOfMaterials] AS Target
USING (
  SELECT [BillOfMaterialsID],[BOMLevel],[ComponentID],[EndDate],[ModifiedDate],[PerAssemblyQty],[ProductAssemblyID],[StartDate],[UnitMeasureCode]
    FROM OPENJSON(@v_json)
    WITH (
           [BillOfMaterialsID] INT,
           [BOMLevel] SMALLINT,
           [ComponentID] INT,
           [EndDate] DATETIME,
           [ModifiedDate] DATETIME,
           [PerAssemblyQty] DECIMAL(8, 2),
           [ProductAssemblyID] INT,
           [StartDate] DATETIME,
           [UnitMeasureCode] NCHAR(3)
    )
) AS Source
ON Source.[BillOfMaterialsID] = Target.[BillOfMaterialsID]

WHEN MATCHED AND (NOT (Target.[BOMLevel] = Source.[BOMLevel] OR (Target.[BOMLevel] IS NULL AND Source.[BOMLevel] IS NULL)) OR NOT (Target.[ComponentID] = Source.[ComponentID] OR (Target.[ComponentID] IS NULL AND Source.[ComponentID] IS NULL)) OR NOT (Target.[EndDate] = Source.[EndDate] OR (Target.[EndDate] IS NULL AND Source.[EndDate] IS NULL)) OR NOT (Target.[ModifiedDate] = Source.[ModifiedDate] OR (Target.[ModifiedDate] IS NULL AND Source.[ModifiedDate] IS NULL)) OR NOT (Target.[PerAssemblyQty] = Source.[PerAssemblyQty] OR (Target.[PerAssemblyQty] IS NULL AND Source.[PerAssemblyQty] IS NULL)) OR NOT (Target.[ProductAssemblyID] = Source.[ProductAssemblyID] OR (Target.[ProductAssemblyID] IS NULL AND Source.[ProductAssemblyID] IS NULL)) OR NOT (Target.[StartDate] = Source.[StartDate] OR (Target.[StartDate] IS NULL AND Source.[StartDate] IS NULL)) OR NOT (Target.[UnitMeasureCode] = Source.[UnitMeasureCode] OR (Target.[UnitMeasureCode] IS NULL AND Source.[UnitMeasureCode] IS NULL))) THEN
  UPDATE SET
        [BOMLevel] = Source.[BOMLevel],
        [ComponentID] = Source.[ComponentID],
        [EndDate] = Source.[EndDate],
        [ModifiedDate] = Source.[ModifiedDate],
        [PerAssemblyQty] = Source.[PerAssemblyQty],
        [ProductAssemblyID] = Source.[ProductAssemblyID],
        [StartDate] = Source.[StartDate],
        [UnitMeasureCode] = Source.[UnitMeasureCode]

 WHEN NOT MATCHED BY TARGET THEN
   INSERT (
         [BillOfMaterialsID],
        [BOMLevel],
        [ComponentID],
        [EndDate],
        [ModifiedDate],
        [PerAssemblyQty],
        [ProductAssemblyID],
        [StartDate],
        [UnitMeasureCode]
   ) VALUES (
         Source.[BillOfMaterialsID],
        Source.[BOMLevel],
        Source.[ComponentID],
        Source.[EndDate],
        Source.[ModifiedDate],
        Source.[PerAssemblyQty],
        Source.[ProductAssemblyID],
        Source.[StartDate],
        Source.[UnitMeasureCode]
   )
 ;
SET IDENTITY_INSERT [Production].[BillOfMaterials] OFF;
