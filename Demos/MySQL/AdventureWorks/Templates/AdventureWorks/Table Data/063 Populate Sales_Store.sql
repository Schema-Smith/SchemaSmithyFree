SET @json_data = '{{Sales_Store.tabledata}}';

INSERT INTO `adventureworks`.`Sales_Store` (`BusinessEntityID`, `Demographics`, `ModifiedDate`, `Name`, `rowguid`, `SalesPersonID`)
SELECT `BusinessEntityID`, `Demographics`, `ModifiedDate`, `Name`, `rowguid`, `SalesPersonID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `Demographics` TEXT PATH '$.Demographics',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalesPersonID` INT PATH '$.SalesPersonID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Demographics` = VALUES(`Demographics`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`),
  `SalesPersonID` = VALUES(`SalesPersonID`);
