SET @json_data = '{{Person_BusinessEntity.tabledata}}';

INSERT INTO `adventureworks`.`Person_BusinessEntity` (`BusinessEntityID`, `ModifiedDate`, `rowguid`)
SELECT `BusinessEntityID`, `ModifiedDate`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`);
