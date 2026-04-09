SET @json_data = '{{Sales_SalesPerson.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesPerson` (`Bonus`, `BusinessEntityID`, `CommissionPct`, `ModifiedDate`, `rowguid`, `SalesLastYear`, `SalesQuota`, `SalesYTD`, `TerritoryID`)
SELECT `Bonus`, `BusinessEntityID`, `CommissionPct`, `ModifiedDate`, `rowguid`, `SalesLastYear`, `SalesQuota`, `SalesYTD`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Bonus` DECIMAL(19,4) PATH '$.Bonus',
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `CommissionPct` DECIMAL(10,4) PATH '$.CommissionPct',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalesLastYear` DECIMAL(19,4) PATH '$.SalesLastYear',
    `SalesQuota` DECIMAL(19,4) PATH '$.SalesQuota',
    `SalesYTD` DECIMAL(19,4) PATH '$.SalesYTD',
    `TerritoryID` INT PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Bonus` = VALUES(`Bonus`),
  `CommissionPct` = VALUES(`CommissionPct`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`),
  `SalesLastYear` = VALUES(`SalesLastYear`),
  `SalesQuota` = VALUES(`SalesQuota`),
  `SalesYTD` = VALUES(`SalesYTD`),
  `TerritoryID` = VALUES(`TerritoryID`);
