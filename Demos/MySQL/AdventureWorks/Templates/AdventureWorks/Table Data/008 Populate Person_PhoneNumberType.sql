SET @json_data = '{{Person_PhoneNumberType.tabledata}}';

INSERT INTO `adventureworks`.`Person_PhoneNumberType` (`ModifiedDate`, `Name`, `PhoneNumberTypeID`)
SELECT `ModifiedDate`, `Name`, `PhoneNumberTypeID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `PhoneNumberTypeID` INT PATH '$.PhoneNumberTypeID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
