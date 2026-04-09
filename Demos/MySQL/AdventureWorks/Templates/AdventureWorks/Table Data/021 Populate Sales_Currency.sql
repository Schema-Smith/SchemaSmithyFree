SET @json_data = '{{Sales_Currency.tabledata}}';

INSERT INTO `adventureworks`.`Sales_Currency` (`CurrencyCode`, `ModifiedDate`, `Name`)
SELECT `CurrencyCode`, `ModifiedDate`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CurrencyCode` CHAR(3) PATH '$.CurrencyCode',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
