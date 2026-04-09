SET @json_data = '{{HumanResources_EmployeePayHistory.tabledata}}';

INSERT INTO `adventureworks`.`HumanResources_EmployeePayHistory` (`BusinessEntityID`, `ModifiedDate`, `PayFrequency`, `Rate`, `RateChangeDate`)
SELECT `BusinessEntityID`, `ModifiedDate`, `PayFrequency`, `Rate`, `RateChangeDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `PayFrequency` TINYINT PATH '$.PayFrequency',
    `Rate` DECIMAL(19,4) PATH '$.Rate',
    `RateChangeDate` DATETIME PATH '$.RateChangeDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `PayFrequency` = VALUES(`PayFrequency`),
  `Rate` = VALUES(`Rate`);
