SET @json_data = '{{Suppliers.tabledata}}';

INSERT INTO `northwind`.`Suppliers` (`Address`, `City`, `CompanyName`, `ContactName`, `ContactTitle`, `Country`, `Fax`, `HomePage`, `Phone`, `PostalCode`, `Region`, `SupplierID`)
SELECT `Address`, `City`, `CompanyName`, `ContactName`, `ContactTitle`, `Country`, `Fax`, `HomePage`, `Phone`, `PostalCode`, `Region`, `SupplierID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Address` VARCHAR(60) PATH '$.Address',
    `City` VARCHAR(15) PATH '$.City',
    `CompanyName` VARCHAR(40) PATH '$.CompanyName',
    `ContactName` VARCHAR(30) PATH '$.ContactName',
    `ContactTitle` VARCHAR(30) PATH '$.ContactTitle',
    `Country` VARCHAR(15) PATH '$.Country',
    `Fax` VARCHAR(24) PATH '$.Fax',
    `HomePage` TEXT PATH '$.HomePage',
    `Phone` VARCHAR(24) PATH '$.Phone',
    `PostalCode` VARCHAR(10) PATH '$.PostalCode',
    `Region` VARCHAR(15) PATH '$.Region',
    `SupplierID` INT PATH '$.SupplierID'
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
  `HomePage` = VALUES(`HomePage`),
  `Phone` = VALUES(`Phone`),
  `PostalCode` = VALUES(`PostalCode`),
  `Region` = VALUES(`Region`);
