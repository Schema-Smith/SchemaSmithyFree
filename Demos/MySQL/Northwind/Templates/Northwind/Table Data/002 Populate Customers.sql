SET @json_data = '{{Customers.tabledata}}';

INSERT INTO `northwind`.`Customers` (`Address`, `City`, `CompanyName`, `ContactName`, `ContactTitle`, `Country`, `CustomerID`, `Fax`, `Phone`, `PostalCode`, `Region`)
SELECT `Address`, `City`, `CompanyName`, `ContactName`, `ContactTitle`, `Country`, `CustomerID`, `Fax`, `Phone`, `PostalCode`, `Region`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Address` VARCHAR(60) PATH '$.Address',
    `City` VARCHAR(15) PATH '$.City',
    `CompanyName` VARCHAR(40) PATH '$.CompanyName',
    `ContactName` VARCHAR(30) PATH '$.ContactName',
    `ContactTitle` VARCHAR(30) PATH '$.ContactTitle',
    `Country` VARCHAR(15) PATH '$.Country',
    `CustomerID` CHAR(5) PATH '$.CustomerID',
    `Fax` VARCHAR(24) PATH '$.Fax',
    `Phone` VARCHAR(24) PATH '$.Phone',
    `PostalCode` VARCHAR(10) PATH '$.PostalCode',
    `Region` VARCHAR(15) PATH '$.Region'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Address` = VALUES(`Address`),
  `City` = VALUES(`City`),
  `CompanyName` = VALUES(`CompanyName`),
  `ContactName` = VALUES(`ContactName`),
  `ContactTitle` = VALUES(`ContactTitle`),
  `Country` = VALUES(`Country`),
  `Fax` = VALUES(`Fax`),
  `Phone` = VALUES(`Phone`),
  `PostalCode` = VALUES(`PostalCode`),
  `Region` = VALUES(`Region`);
