SET @json_data = '{{Sales_Customer.tabledata}}';

INSERT INTO `adventureworks`.`Sales_Customer` (`CustomerID`, `ModifiedDate`, `PersonID`, `rowguid`, `StoreID`, `TerritoryID`)
SELECT `CustomerID`, `ModifiedDate`, `PersonID`, `rowguid`, `StoreID`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CustomerID` INT PATH '$.CustomerID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `PersonID` INT PATH '$.PersonID',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `StoreID` INT PATH '$.StoreID',
    `TerritoryID` INT PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `PersonID` = VALUES(`PersonID`),
  `rowguid` = VALUES(`rowguid`),
  `StoreID` = VALUES(`StoreID`),
  `TerritoryID` = VALUES(`TerritoryID`);
