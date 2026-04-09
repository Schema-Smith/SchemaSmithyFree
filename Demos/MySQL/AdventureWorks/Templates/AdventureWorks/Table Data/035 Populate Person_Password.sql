SET @json_data = '{{Person_Password.tabledata}}';

INSERT INTO `adventureworks`.`Person_Password` (`BusinessEntityID`, `ModifiedDate`, `PasswordHash`, `PasswordSalt`, `rowguid`)
SELECT `BusinessEntityID`, `ModifiedDate`, `PasswordHash`, `PasswordSalt`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `BusinessEntityID` INT PATH '$.BusinessEntityID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `PasswordHash` VARCHAR(128) PATH '$.PasswordHash',
    `PasswordSalt` VARCHAR(10) PATH '$.PasswordSalt',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `PasswordHash` = VALUES(`PasswordHash`),
  `PasswordSalt` = VALUES(`PasswordSalt`),
  `rowguid` = VALUES(`rowguid`);
