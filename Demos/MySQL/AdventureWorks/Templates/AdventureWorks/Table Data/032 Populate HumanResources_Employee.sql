SET @json_data = '{{HumanResources_Employee.tabledata}}';

INSERT INTO `adventureworks`.`HumanResources_Employee` (`BirthDate`, `BusinessEntityID`, `CurrentFlag`, `Gender`, `HireDate`, `JobTitle`, `LoginID`, `MaritalStatus`, `ModifiedDate`, `NationalIDNumber`, `OrganizationNode`, `rowguid`, `SalariedFlag`, `SickLeaveHours`, `VacationHours`)
SELECT `BirthDate`, `BusinessEntityID`, `CurrentFlag`, `Gender`, `HireDate`, `JobTitle`, `LoginID`, `MaritalStatus`, `ModifiedDate`, `NationalIDNumber`, `OrganizationNode`, `rowguid`, `SalariedFlag`, `SickLeaveHours`, `VacationHours`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BirthDate` DATE PATH '$.BirthDate',
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `CurrentFlag` TINYINT PATH '$.CurrentFlag',
    `Gender` CHAR(1) PATH '$.Gender',
    `HireDate` DATE PATH '$.HireDate',
    `JobTitle` VARCHAR(50) PATH '$.JobTitle',
    `LoginID` VARCHAR(256) PATH '$.LoginID',
    `MaritalStatus` CHAR(1) PATH '$.MaritalStatus',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `NationalIDNumber` VARCHAR(15) PATH '$.NationalIDNumber',
    `OrganizationNode` VARCHAR(255) PATH '$.OrganizationNode',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalariedFlag` TINYINT PATH '$.SalariedFlag',
    `SickLeaveHours` SMALLINT PATH '$.SickLeaveHours',
    `VacationHours` SMALLINT PATH '$.VacationHours'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `BirthDate` = VALUES(`BirthDate`),
  `CurrentFlag` = VALUES(`CurrentFlag`),
  `Gender` = VALUES(`Gender`),
  `HireDate` = VALUES(`HireDate`),
  `JobTitle` = VALUES(`JobTitle`),
  `LoginID` = VALUES(`LoginID`),
  `MaritalStatus` = VALUES(`MaritalStatus`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `NationalIDNumber` = VALUES(`NationalIDNumber`),
  `OrganizationNode` = VALUES(`OrganizationNode`),
  `rowguid` = VALUES(`rowguid`),
  `SalariedFlag` = VALUES(`SalariedFlag`),
  `SickLeaveHours` = VALUES(`SickLeaveHours`),
  `VacationHours` = VALUES(`VacationHours`);
