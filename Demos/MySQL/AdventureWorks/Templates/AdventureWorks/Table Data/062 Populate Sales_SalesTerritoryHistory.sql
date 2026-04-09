SET @json_data = '{{Sales_SalesTerritoryHistory.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesTerritoryHistory` (`BusinessEntityID`, `EndDate`, `ModifiedDate`, `rowguid`, `StartDate`, `TerritoryID`)
SELECT `BusinessEntityID`, `EndDate`, `ModifiedDate`, `rowguid`, `StartDate`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `EndDate` DATETIME PATH '$.EndDate',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `StartDate` DATETIME PATH '$.StartDate',
    `TerritoryID` INT PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `EndDate` = VALUES(`EndDate`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`);
