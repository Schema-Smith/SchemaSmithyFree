SET @json_data = '{{Person_BusinessEntityContact.tabledata}}';

INSERT INTO `adventureworks`.`Person_BusinessEntityContact` (`BusinessEntityID`, `ContactTypeID`, `ModifiedDate`, `PersonID`, `rowguid`)
SELECT `BusinessEntityID`, `ContactTypeID`, `ModifiedDate`, `PersonID`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `ContactTypeID` INT PATH '$.ContactTypeID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `PersonID` INT PATH '$.PersonID',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`);
