SET @json_data = '{{Person_ContactType.tabledata}}';

INSERT INTO `adventureworks`.`Person_ContactType` (`ContactTypeID`, `ModifiedDate`, `Name`)
SELECT `ContactTypeID`, `ModifiedDate`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ContactTypeID` INT PATH '$.ContactTypeID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
