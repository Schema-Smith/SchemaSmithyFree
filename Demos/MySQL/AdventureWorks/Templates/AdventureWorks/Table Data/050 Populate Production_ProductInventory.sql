SET @json_data = '{{Production_ProductInventory.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductInventory` (`Bin`, `LocationID`, `ModifiedDate`, `ProductID`, `Quantity`, `rowguid`, `Shelf`)
SELECT `Bin`, `LocationID`, `ModifiedDate`, `ProductID`, `Quantity`, `rowguid`, `Shelf`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Bin` TINYINT PATH '$.Bin',
    `LocationID` SMALLINT PATH '$.LocationID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID',
    `Quantity` SMALLINT PATH '$.Quantity',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `Shelf` VARCHAR(10) PATH '$.Shelf'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Bin` = VALUES(`Bin`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Quantity` = VALUES(`Quantity`),
  `rowguid` = VALUES(`rowguid`),
  `Shelf` = VALUES(`Shelf`);
