SET @json_data = '{{Purchasing_ProductVendor.tabledata}}';

INSERT INTO `adventureworks`.`Purchasing_ProductVendor` (`AverageLeadTime`, `BusinessEntityID`, `LastReceiptCost`, `LastReceiptDate`, `MaxOrderQty`, `MinOrderQty`, `ModifiedDate`, `OnOrderQty`, `ProductID`, `StandardPrice`, `UnitMeasureCode`)
SELECT `AverageLeadTime`, `BusinessEntityID`, `LastReceiptCost`, `LastReceiptDate`, `MaxOrderQty`, `MinOrderQty`, `ModifiedDate`, `OnOrderQty`, `ProductID`, `StandardPrice`, `UnitMeasureCode`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AverageLeadTime` INT PATH '$.AverageLeadTime',
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `LastReceiptCost` DECIMAL(19,4) PATH '$.LastReceiptCost',
    `LastReceiptDate` DATETIME PATH '$.LastReceiptDate',
    `MaxOrderQty` INT PATH '$.MaxOrderQty',
    `MinOrderQty` INT PATH '$.MinOrderQty',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `OnOrderQty` INT PATH '$.OnOrderQty',
    `ProductID` INT PATH '$.ProductID',
    `StandardPrice` DECIMAL(19,4) PATH '$.StandardPrice',
    `UnitMeasureCode` CHAR(3) PATH '$.UnitMeasureCode'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `AverageLeadTime` = VALUES(`AverageLeadTime`),
  `LastReceiptCost` = VALUES(`LastReceiptCost`),
  `LastReceiptDate` = VALUES(`LastReceiptDate`),
  `MaxOrderQty` = VALUES(`MaxOrderQty`),
  `MinOrderQty` = VALUES(`MinOrderQty`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `OnOrderQty` = VALUES(`OnOrderQty`),
  `StandardPrice` = VALUES(`StandardPrice`),
  `UnitMeasureCode` = VALUES(`UnitMeasureCode`);
