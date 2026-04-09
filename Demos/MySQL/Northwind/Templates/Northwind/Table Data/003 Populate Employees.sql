SET @json_data = '{{Employees.tabledata}}';

INSERT INTO `northwind`.`Employees` (`Address`, `BirthDate`, `City`, `Country`, `EmployeeID`, `Extension`, `FirstName`, `HireDate`, `HomePhone`, `LastName`, `Notes`, `Photo`, `PhotoPath`, `PostalCode`, `Region`, `ReportsTo`, `Title`, `TitleOfCourtesy`)
SELECT `Address`, `BirthDate`, `City`, `Country`, `EmployeeID`, `Extension`, `FirstName`, `HireDate`, `HomePhone`, `LastName`, `Notes`, FROM_BASE64(`Photo`), `PhotoPath`, `PostalCode`, `Region`, `ReportsTo`, `Title`, `TitleOfCourtesy`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Address` VARCHAR(60) PATH '$.Address',
    `BirthDate` DATETIME PATH '$.BirthDate',
    `City` VARCHAR(15) PATH '$.City',
    `Country` VARCHAR(15) PATH '$.Country',
    `EmployeeID` INT PATH '$.EmployeeID',
    `Extension` VARCHAR(4) PATH '$.Extension',
    `FirstName` VARCHAR(10) PATH '$.FirstName',
    `HireDate` DATETIME PATH '$.HireDate',
    `HomePhone` VARCHAR(24) PATH '$.HomePhone',
    `LastName` VARCHAR(20) PATH '$.LastName',
    `Notes` TEXT PATH '$.Notes',
    `Photo` TEXT PATH '$.Photo',
    `PhotoPath` VARCHAR(255) PATH '$.PhotoPath',
    `PostalCode` VARCHAR(10) PATH '$.PostalCode',
    `Region` VARCHAR(15) PATH '$.Region',
    `ReportsTo` INT PATH '$.ReportsTo',
    `Title` VARCHAR(30) PATH '$.Title',
    `TitleOfCourtesy` VARCHAR(25) PATH '$.TitleOfCourtesy'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Address` = VALUES(`Address`),
  `BirthDate` = VALUES(`BirthDate`),
  `City` = VALUES(`City`),
  `Country` = VALUES(`Country`),
  `Extension` = VALUES(`Extension`),
  `FirstName` = VALUES(`FirstName`),
  `HireDate` = VALUES(`HireDate`),
  `HomePhone` = VALUES(`HomePhone`),
  `LastName` = VALUES(`LastName`),
  `Notes` = VALUES(`Notes`),
  `Photo` = VALUES(`Photo`),
  `PhotoPath` = VALUES(`PhotoPath`),
  `PostalCode` = VALUES(`PostalCode`),
  `Region` = VALUES(`Region`),
  `ReportsTo` = VALUES(`ReportsTo`),
  `Title` = VALUES(`Title`),
  `TitleOfCourtesy` = VALUES(`TitleOfCourtesy`);
