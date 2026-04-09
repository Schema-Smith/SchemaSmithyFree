SET @json_data = '{{Person_EmailAddress.tabledata}}';

INSERT INTO `adventureworks`.`Person_EmailAddress` (`BusinessEntityID`, `EmailAddress`, `EmailAddressID`, `ModifiedDate`, `rowguid`)
SELECT `BusinessEntityID`, `EmailAddress`, `EmailAddressID`, `ModifiedDate`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `EmailAddress` VARCHAR(50) PATH '$.EmailAddress',
    `EmailAddressID` INT PATH '$.EmailAddressID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `EmailAddress` = VALUES(`EmailAddress`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`);
