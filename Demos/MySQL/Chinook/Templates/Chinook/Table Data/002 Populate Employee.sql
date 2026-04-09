SET @json_data = '{{Employee.tabledata}}';

INSERT INTO `Chinook`.`Employee` (`EmployeeId`, `LastName`, `FirstName`, `Title`, `ReportsTo`, `BirthDate`, `HireDate`, `Address`, `City`, `State`, `Country`, `PostalCode`, `Phone`, `Fax`, `Email`)
SELECT `EmployeeId`, `LastName`, `FirstName`, `Title`, `ReportsTo`, `BirthDate`, `HireDate`, `Address`, `City`, `State`, `Country`, `PostalCode`, `Phone`, `Fax`, `Email`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `EmployeeId` INT PATH '$.EmployeeId',
    `LastName` VARCHAR(20) PATH '$.LastName',
    `FirstName` VARCHAR(20) PATH '$.FirstName',
    `Title` VARCHAR(30) PATH '$.Title',
    `ReportsTo` INT PATH '$.ReportsTo',
    `BirthDate` DATETIME PATH '$.BirthDate',
    `HireDate` DATETIME PATH '$.HireDate',
    `Address` VARCHAR(70) PATH '$.Address',
    `City` VARCHAR(40) PATH '$.City',
    `State` VARCHAR(40) PATH '$.State',
    `Country` VARCHAR(40) PATH '$.Country',
    `PostalCode` VARCHAR(10) PATH '$.PostalCode',
    `Phone` VARCHAR(24) PATH '$.Phone',
    `Fax` VARCHAR(24) PATH '$.Fax',
    `Email` VARCHAR(60) PATH '$.Email'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `LastName` = VALUES(`LastName`),
  `FirstName` = VALUES(`FirstName`),
  `Title` = VALUES(`Title`),
  `ReportsTo` = VALUES(`ReportsTo`),
  `BirthDate` = VALUES(`BirthDate`),
  `HireDate` = VALUES(`HireDate`),
  `Address` = VALUES(`Address`),
  `City` = VALUES(`City`),
  `State` = VALUES(`State`),
  `Country` = VALUES(`Country`),
  `PostalCode` = VALUES(`PostalCode`),
  `Phone` = VALUES(`Phone`),
  `Fax` = VALUES(`Fax`),
  `Email` = VALUES(`Email`);
