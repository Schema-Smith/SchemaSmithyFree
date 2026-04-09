SET @json_data = '{{Person_BusinessEntityAddress.tabledata}}';

INSERT INTO `adventureworks`.`Person_BusinessEntityAddress` (`AddressID`, `AddressTypeID`, `BusinessEntityID`, `ModifiedDate`, `rowguid`)
SELECT `AddressID`, `AddressTypeID`, `BusinessEntityID`, `ModifiedDate`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AddressID` INT PATH '$.AddressID',
    `AddressTypeID` INT PATH '$.AddressTypeID',
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`);
