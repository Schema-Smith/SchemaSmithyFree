SET @json_data = '{{Sales_SalesTerritory.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesTerritory` (`CostLastYear`, `CostYTD`, `CountryRegionCode`, `Group`, `ModifiedDate`, `Name`, `rowguid`, `SalesLastYear`, `SalesYTD`, `TerritoryID`)
SELECT `CostLastYear`, `CostYTD`, `CountryRegionCode`, `Group`, `ModifiedDate`, `Name`, `rowguid`, `SalesLastYear`, `SalesYTD`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CostLastYear` DECIMAL(19,4) PATH '$.CostLastYear',
    `CostYTD` DECIMAL(19,4) PATH '$.CostYTD',
    `CountryRegionCode` VARCHAR(3) PATH '$.CountryRegionCode',
    `Group` VARCHAR(50) PATH '$.Group',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalesLastYear` DECIMAL(19,4) PATH '$.SalesLastYear',
    `SalesYTD` DECIMAL(19,4) PATH '$.SalesYTD',
    `TerritoryID` INT PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CostLastYear` = VALUES(`CostLastYear`),
  `CostYTD` = VALUES(`CostYTD`),
  `CountryRegionCode` = VALUES(`CountryRegionCode`),
  `Group` = VALUES(`Group`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`),
  `SalesLastYear` = VALUES(`SalesLastYear`),
  `SalesYTD` = VALUES(`SalesYTD`);
