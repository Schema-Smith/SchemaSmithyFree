SET @json_data = '{{Person_AddressType.tabledata}}';

INSERT INTO `adventureworks`.`Person_AddressType` (`AddressTypeID`, `ModifiedDate`, `Name`, `rowguid`)
SELECT `AddressTypeID`, `ModifiedDate`, `Name`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AddressTypeID` INT PATH '$.AddressTypeID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`);
