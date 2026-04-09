SET @json_data = '{{Production_Product.tabledata}}';

INSERT INTO `adventureworks`.`Production_Product` (`Class`, `Color`, `DaysToManufacture`, `DiscontinuedDate`, `FinishedGoodsFlag`, `ListPrice`, `MakeFlag`, `ModifiedDate`, `Name`, `ProductID`, `ProductLine`, `ProductModelID`, `ProductNumber`, `ProductSubcategoryID`, `ReorderPoint`, `rowguid`, `SafetyStockLevel`, `SellEndDate`, `SellStartDate`, `Size`, `SizeUnitMeasureCode`, `StandardCost`, `Style`, `Weight`, `WeightUnitMeasureCode`)
SELECT `Class`, `Color`, `DaysToManufacture`, `DiscontinuedDate`, `FinishedGoodsFlag`, `ListPrice`, `MakeFlag`, `ModifiedDate`, `Name`, `ProductID`, `ProductLine`, `ProductModelID`, `ProductNumber`, `ProductSubcategoryID`, `ReorderPoint`, `rowguid`, `SafetyStockLevel`, `SellEndDate`, `SellStartDate`, `Size`, `SizeUnitMeasureCode`, `StandardCost`, `Style`, `Weight`, `WeightUnitMeasureCode`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Class` CHAR(2) PATH '$.Class',
    `Color` VARCHAR(15) PATH '$.Color',
    `DaysToManufacture` INT PATH '$.DaysToManufacture',
    `DiscontinuedDate` DATETIME PATH '$.DiscontinuedDate',
    `FinishedGoodsFlag` TINYINT PATH '$.FinishedGoodsFlag',
    `ListPrice` DECIMAL(19,4) PATH '$.ListPrice',
    `MakeFlag` TINYINT PATH '$.MakeFlag',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `ProductID` INT PATH '$.ProductID',
    `ProductLine` CHAR(2) PATH '$.ProductLine',
    `ProductModelID` INT PATH '$.ProductModelID',
    `ProductNumber` VARCHAR(25) PATH '$.ProductNumber',
    `ProductSubcategoryID` INT PATH '$.ProductSubcategoryID',
    `ReorderPoint` SMALLINT PATH '$.ReorderPoint',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SafetyStockLevel` SMALLINT PATH '$.SafetyStockLevel',
    `SellEndDate` DATETIME PATH '$.SellEndDate',
    `SellStartDate` DATETIME PATH '$.SellStartDate',
    `Size` VARCHAR(5) PATH '$.Size',
    `SizeUnitMeasureCode` CHAR(3) PATH '$.SizeUnitMeasureCode',
    `StandardCost` DECIMAL(19,4) PATH '$.StandardCost',
    `Style` CHAR(2) PATH '$.Style',
    `Weight` DECIMAL(8,2) PATH '$.Weight',
    `WeightUnitMeasureCode` CHAR(3) PATH '$.WeightUnitMeasureCode'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Class` = VALUES(`Class`),
  `Color` = VALUES(`Color`),
  `DaysToManufacture` = VALUES(`DaysToManufacture`),
  `DiscontinuedDate` = VALUES(`DiscontinuedDate`),
  `FinishedGoodsFlag` = VALUES(`FinishedGoodsFlag`),
  `ListPrice` = VALUES(`ListPrice`),
  `MakeFlag` = VALUES(`MakeFlag`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `ProductLine` = VALUES(`ProductLine`),
  `ProductModelID` = VALUES(`ProductModelID`),
  `ProductNumber` = VALUES(`ProductNumber`),
  `ProductSubcategoryID` = VALUES(`ProductSubcategoryID`),
  `ReorderPoint` = VALUES(`ReorderPoint`),
  `rowguid` = VALUES(`rowguid`),
  `SafetyStockLevel` = VALUES(`SafetyStockLevel`),
  `SellEndDate` = VALUES(`SellEndDate`),
  `SellStartDate` = VALUES(`SellStartDate`),
  `Size` = VALUES(`Size`),
  `SizeUnitMeasureCode` = VALUES(`SizeUnitMeasureCode`),
  `StandardCost` = VALUES(`StandardCost`),
  `Style` = VALUES(`Style`),
  `Weight` = VALUES(`Weight`),
  `WeightUnitMeasureCode` = VALUES(`WeightUnitMeasureCode`);
