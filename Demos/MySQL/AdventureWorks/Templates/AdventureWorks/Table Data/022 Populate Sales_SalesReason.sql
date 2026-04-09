SET @json_data = '{{Sales_SalesReason.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesReason` (`ModifiedDate`, `Name`, `ReasonType`, `SalesReasonID`)
SELECT `ModifiedDate`, `Name`, `ReasonType`, `SalesReasonID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `ReasonType` VARCHAR(50) PATH '$.ReasonType',
    `SalesReasonID` INT PATH '$.SalesReasonID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `ReasonType` = VALUES(`ReasonType`);
