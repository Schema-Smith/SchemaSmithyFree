SET @json_data = '{{Person_CountryRegion.tabledata}}';

INSERT INTO `adventureworks`.`Person_CountryRegion` (`CountryRegionCode`, `ModifiedDate`, `Name`)
SELECT `CountryRegionCode`, `ModifiedDate`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CountryRegionCode` VARCHAR(3) PATH '$.CountryRegionCode',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
