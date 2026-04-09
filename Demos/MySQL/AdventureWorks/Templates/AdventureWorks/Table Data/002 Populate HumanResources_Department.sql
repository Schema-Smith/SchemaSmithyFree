SET @json_data = '{{HumanResources_Department.tabledata}}';

INSERT INTO `adventureworks`.`HumanResources_Department` (`DepartmentID`, `GroupName`, `ModifiedDate`, `Name`)
SELECT `DepartmentID`, `GroupName`, `ModifiedDate`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `DepartmentID` SMALLINT PATH '$.DepartmentID',
    `GroupName` VARCHAR(50) PATH '$.GroupName',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `GroupName` = VALUES(`GroupName`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
