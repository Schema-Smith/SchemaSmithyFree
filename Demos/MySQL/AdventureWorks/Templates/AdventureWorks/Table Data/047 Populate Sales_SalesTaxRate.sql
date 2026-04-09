SET @json_data = '{{Sales_SalesTaxRate.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesTaxRate` (`ModifiedDate`, `Name`, `rowguid`, `SalesTaxRateID`, `StateProvinceID`, `TaxRate`, `TaxType`)
SELECT `ModifiedDate`, `Name`, `rowguid`, `SalesTaxRateID`, `StateProvinceID`, `TaxRate`, `TaxType`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalesTaxRateID` INT PATH '$.SalesTaxRateID',
    `StateProvinceID` INT PATH '$.StateProvinceID',
    `TaxRate` DECIMAL(10,4) PATH '$.TaxRate',
    `TaxType` TINYINT PATH '$.TaxType'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`),
  `StateProvinceID` = VALUES(`StateProvinceID`),
  `TaxRate` = VALUES(`TaxRate`),
  `TaxType` = VALUES(`TaxType`);
