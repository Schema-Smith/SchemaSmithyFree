SET @json_data = '{{Customer.tabledata}}';

INSERT INTO `Chinook`.`Customer` (`CustomerId`, `FirstName`, `LastName`, `Company`, `Address`, `City`, `State`, `Country`, `PostalCode`, `Phone`, `Fax`, `Email`, `SupportRepId`)
SELECT `CustomerId`, `FirstName`, `LastName`, `Company`, `Address`, `City`, `State`, `Country`, `PostalCode`, `Phone`, `Fax`, `Email`, `SupportRepId`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CustomerId` INT PATH '$.CustomerId',
    `FirstName` VARCHAR(40) PATH '$.FirstName',
    `LastName` VARCHAR(20) PATH '$.LastName',
    `Company` VARCHAR(80) PATH '$.Company',
    `Address` VARCHAR(70) PATH '$.Address',
    `City` VARCHAR(40) PATH '$.City',
    `State` VARCHAR(40) PATH '$.State',
    `Country` VARCHAR(40) PATH '$.Country',
    `PostalCode` VARCHAR(10) PATH '$.PostalCode',
    `Phone` VARCHAR(24) PATH '$.Phone',
    `Fax` VARCHAR(24) PATH '$.Fax',
    `Email` VARCHAR(60) PATH '$.Email',
    `SupportRepId` INT PATH '$.SupportRepId'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `FirstName` = VALUES(`FirstName`),
  `LastName` = VALUES(`LastName`),
  `Company` = VALUES(`Company`),
  `Address` = VALUES(`Address`),
  `City` = VALUES(`City`),
  `State` = VALUES(`State`),
  `Country` = VALUES(`Country`),
  `PostalCode` = VALUES(`PostalCode`),
  `Phone` = VALUES(`Phone`),
  `Fax` = VALUES(`Fax`),
  `Email` = VALUES(`Email`),
  `SupportRepId` = VALUES(`SupportRepId`);
