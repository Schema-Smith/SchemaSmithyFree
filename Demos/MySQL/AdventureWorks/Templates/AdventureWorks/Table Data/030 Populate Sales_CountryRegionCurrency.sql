SET @json_data = '{{Sales_CountryRegionCurrency.tabledata}}';

INSERT INTO `adventureworks`.`Sales_CountryRegionCurrency` (`CountryRegionCode`, `CurrencyCode`, `ModifiedDate`)
SELECT `CountryRegionCode`, `CurrencyCode`, `ModifiedDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CountryRegionCode` VARCHAR(3) PATH '$.CountryRegionCode',
    `CurrencyCode` CHAR(3) PATH '$.CurrencyCode',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`);
