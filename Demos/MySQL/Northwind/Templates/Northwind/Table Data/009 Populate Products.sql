SET @json_data = '{{Products.tabledata}}';

INSERT INTO `northwind`.`Products` (`CategoryID`, `Discontinued`, `ProductID`, `ProductName`, `QuantityPerUnit`, `ReorderLevel`, `SupplierID`, `UnitPrice`, `UnitsInStock`, `UnitsOnOrder`)
SELECT `CategoryID`, `Discontinued`, `ProductID`, `ProductName`, `QuantityPerUnit`, `ReorderLevel`, `SupplierID`, `UnitPrice`, `UnitsInStock`, `UnitsOnOrder`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CategoryID` INT PATH '$.CategoryID',
    `Discontinued` TINYINT PATH '$.Discontinued',
    `ProductID` INT PATH '$.ProductID',
    `ProductName` VARCHAR(40) PATH '$.ProductName',
    `QuantityPerUnit` VARCHAR(20) PATH '$.QuantityPerUnit',
    `ReorderLevel` SMALLINT PATH '$.ReorderLevel',
    `SupplierID` INT PATH '$.SupplierID',
    `UnitPrice` DECIMAL(19,4) PATH '$.UnitPrice',
    `UnitsInStock` SMALLINT PATH '$.UnitsInStock',
    `UnitsOnOrder` SMALLINT PATH '$.UnitsOnOrder'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CategoryID` = VALUES(`CategoryID`),
  `Discontinued` = VALUES(`Discontinued`),
  `ProductName` = VALUES(`ProductName`),
  `QuantityPerUnit` = VALUES(`QuantityPerUnit`),
  `ReorderLevel` = VALUES(`ReorderLevel`),
  `SupplierID` = VALUES(`SupplierID`),
  `UnitPrice` = VALUES(`UnitPrice`),
  `UnitsInStock` = VALUES(`UnitsInStock`),
  `UnitsOnOrder` = VALUES(`UnitsOnOrder`);
