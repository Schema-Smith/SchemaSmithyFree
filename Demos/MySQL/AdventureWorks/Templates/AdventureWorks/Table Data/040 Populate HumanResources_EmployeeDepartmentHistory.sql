SET @json_data = '{{HumanResources_EmployeeDepartmentHistory.tabledata}}';

INSERT INTO `adventureworks`.`HumanResources_EmployeeDepartmentHistory` (`BusinessEntityID`, `DepartmentID`, `EndDate`, `ModifiedDate`, `ShiftID`, `StartDate`)
SELECT `BusinessEntityID`, `DepartmentID`, `EndDate`, `ModifiedDate`, `ShiftID`, `StartDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `DepartmentID` SMALLINT PATH '$.DepartmentID',
    `EndDate` DATE PATH '$.EndDate',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ShiftID` TINYINT PATH '$.ShiftID',
    `StartDate` DATE PATH '$.StartDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `EndDate` = VALUES(`EndDate`),
  `ModifiedDate` = VALUES(`ModifiedDate`);
