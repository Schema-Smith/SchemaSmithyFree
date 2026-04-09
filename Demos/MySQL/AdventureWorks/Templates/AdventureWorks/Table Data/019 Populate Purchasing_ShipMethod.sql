SET @json_data = '{{Purchasing_ShipMethod.tabledata}}';

INSERT INTO `adventureworks`.`Purchasing_ShipMethod` (`ModifiedDate`, `Name`, `rowguid`, `ShipBase`, `ShipMethodID`, `ShipRate`)
SELECT `ModifiedDate`, `Name`, `rowguid`, `ShipBase`, `ShipMethodID`, `ShipRate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `ShipBase` DECIMAL(19,4) PATH '$.ShipBase',
    `ShipMethodID` INT PATH '$.ShipMethodID',
    `ShipRate` DECIMAL(19,4) PATH '$.ShipRate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`),
  `ShipBase` = VALUES(`ShipBase`),
  `ShipRate` = VALUES(`ShipRate`);
