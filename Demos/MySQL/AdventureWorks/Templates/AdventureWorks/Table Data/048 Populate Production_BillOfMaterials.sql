SET @json_data = '{{Production_BillOfMaterials.tabledata}}';

INSERT INTO `adventureworks`.`Production_BillOfMaterials` (`BillOfMaterialsID`, `BOMLevel`, `ComponentID`, `EndDate`, `ModifiedDate`, `PerAssemblyQty`, `ProductAssemblyID`, `StartDate`, `UnitMeasureCode`)
SELECT `BillOfMaterialsID`, `BOMLevel`, `ComponentID`, `EndDate`, `ModifiedDate`, `PerAssemblyQty`, `ProductAssemblyID`, `StartDate`, `UnitMeasureCode`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BillOfMaterialsID` INT PATH '$.BillOfMaterialsID',
    `BOMLevel` SMALLINT PATH '$.BOMLevel',
    `ComponentID` INT PATH '$.ComponentID',
    `EndDate` DATETIME PATH '$.EndDate',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `PerAssemblyQty` DECIMAL(8,2) PATH '$.PerAssemblyQty',
    `ProductAssemblyID` INT PATH '$.ProductAssemblyID',
    `StartDate` DATETIME PATH '$.StartDate',
    `UnitMeasureCode` CHAR(3) PATH '$.UnitMeasureCode'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `BOMLevel` = VALUES(`BOMLevel`),
  `ComponentID` = VALUES(`ComponentID`),
  `EndDate` = VALUES(`EndDate`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `PerAssemblyQty` = VALUES(`PerAssemblyQty`),
  `ProductAssemblyID` = VALUES(`ProductAssemblyID`),
  `StartDate` = VALUES(`StartDate`),
  `UnitMeasureCode` = VALUES(`UnitMeasureCode`);
