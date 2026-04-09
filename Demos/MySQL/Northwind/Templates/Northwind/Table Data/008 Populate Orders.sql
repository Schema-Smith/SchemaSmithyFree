SET @json_data = '{{Orders.tabledata}}';

INSERT INTO `northwind`.`Orders` (`CustomerID`, `EmployeeID`, `Freight`, `OrderDate`, `OrderID`, `RequiredDate`, `ShipAddress`, `ShipCity`, `ShipCountry`, `ShipName`, `ShippedDate`, `ShipPostalCode`, `ShipRegion`, `ShipVia`)
SELECT `CustomerID`, `EmployeeID`, `Freight`, `OrderDate`, `OrderID`, `RequiredDate`, `ShipAddress`, `ShipCity`, `ShipCountry`, `ShipName`, `ShippedDate`, `ShipPostalCode`, `ShipRegion`, `ShipVia`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CustomerID` CHAR(5) PATH '$.CustomerID',
    `EmployeeID` INT PATH '$.EmployeeID',
    `Freight` DECIMAL(19,4) PATH '$.Freight',
    `OrderDate` DATETIME PATH '$.OrderDate',
    `OrderID` INT PATH '$.OrderID',
    `RequiredDate` DATETIME PATH '$.RequiredDate',
    `ShipAddress` VARCHAR(60) PATH '$.ShipAddress',
    `ShipCity` VARCHAR(15) PATH '$.ShipCity',
    `ShipCountry` VARCHAR(15) PATH '$.ShipCountry',
    `ShipName` VARCHAR(40) PATH '$.ShipName',
    `ShippedDate` DATETIME PATH '$.ShippedDate',
    `ShipPostalCode` VARCHAR(10) PATH '$.ShipPostalCode',
    `ShipRegion` VARCHAR(15) PATH '$.ShipRegion',
    `ShipVia` INT PATH '$.ShipVia'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CustomerID` = VALUES(`CustomerID`),
  `EmployeeID` = VALUES(`EmployeeID`),
  `Freight` = VALUES(`Freight`),
  `OrderDate` = VALUES(`OrderDate`),
  `RequiredDate` = VALUES(`RequiredDate`),
  `ShipAddress` = VALUES(`ShipAddress`),
  `ShipCity` = VALUES(`ShipCity`),
  `ShipCountry` = VALUES(`ShipCountry`),
  `ShipName` = VALUES(`ShipName`),
  `ShippedDate` = VALUES(`ShippedDate`),
  `ShipPostalCode` = VALUES(`ShipPostalCode`),
  `ShipRegion` = VALUES(`ShipRegion`),
  `ShipVia` = VALUES(`ShipVia`);
