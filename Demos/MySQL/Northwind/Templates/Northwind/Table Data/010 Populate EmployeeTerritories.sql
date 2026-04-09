SET @json_data = '{{EmployeeTerritories.tabledata}}';

INSERT INTO `northwind`.`EmployeeTerritories` (`EmployeeID`, `TerritoryID`)
SELECT `EmployeeID`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `EmployeeID` INT PATH '$.EmployeeID',
    `TerritoryID` VARCHAR(20) PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE `EmployeeID` = VALUES(`EmployeeID`);
