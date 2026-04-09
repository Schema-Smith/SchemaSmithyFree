SET @json_data = '{{Person_Address.tabledata}}';

INSERT INTO `adventureworks`.`Person_Address` (`AddressID`, `AddressLine1`, `AddressLine2`, `City`, `ModifiedDate`, `PostalCode`, `rowguid`, `SpatialLocation`, `StateProvinceID`)
SELECT `AddressID`, `AddressLine1`, `AddressLine2`, `City`, `ModifiedDate`, `PostalCode`, `rowguid`, `SpatialLocation`, `StateProvinceID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AddressID` INT PATH '$.AddressID',
    `AddressLine1` VARCHAR(60) PATH '$.AddressLine1',
    `AddressLine2` VARCHAR(60) PATH '$.AddressLine2',
    `City` VARCHAR(30) PATH '$.City',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `PostalCode` VARCHAR(15) PATH '$.PostalCode',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SpatialLocation` TEXT PATH '$.SpatialLocation',
    `StateProvinceID` INT PATH '$.StateProvinceID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `AddressLine1` = VALUES(`AddressLine1`),
  `AddressLine2` = VALUES(`AddressLine2`),
  `City` = VALUES(`City`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `PostalCode` = VALUES(`PostalCode`),
  `rowguid` = VALUES(`rowguid`),
  `SpatialLocation` = VALUES(`SpatialLocation`),
  `StateProvinceID` = VALUES(`StateProvinceID`);
