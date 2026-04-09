SET @json_data = '{{Person_PersonPhone.tabledata}}';

INSERT INTO `adventureworks`.`Person_PersonPhone` (`BusinessEntityID`, `ModifiedDate`, `PhoneNumber`, `PhoneNumberTypeID`)
SELECT `BusinessEntityID`, `ModifiedDate`, `PhoneNumber`, `PhoneNumberTypeID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `PhoneNumber` VARCHAR(25) PATH '$.PhoneNumber',
    `PhoneNumberTypeID` INT PATH '$.PhoneNumberTypeID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`);
