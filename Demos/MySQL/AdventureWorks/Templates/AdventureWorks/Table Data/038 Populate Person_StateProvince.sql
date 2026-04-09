SET @json_data = '{{Person_StateProvince.tabledata}}';

INSERT INTO `adventureworks`.`Person_StateProvince` (`CountryRegionCode`, `IsOnlyStateProvinceFlag`, `ModifiedDate`, `Name`, `rowguid`, `StateProvinceCode`, `StateProvinceID`, `TerritoryID`)
SELECT `CountryRegionCode`, `IsOnlyStateProvinceFlag`, `ModifiedDate`, `Name`, `rowguid`, `StateProvinceCode`, `StateProvinceID`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CountryRegionCode` VARCHAR(3) PATH '$.CountryRegionCode',
    `IsOnlyStateProvinceFlag` TINYINT PATH '$.IsOnlyStateProvinceFlag',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `StateProvinceCode` CHAR(3) PATH '$.StateProvinceCode',
    `StateProvinceID` INT PATH '$.StateProvinceID',
    `TerritoryID` INT PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CountryRegionCode` = VALUES(`CountryRegionCode`),
  `IsOnlyStateProvinceFlag` = VALUES(`IsOnlyStateProvinceFlag`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`),
  `StateProvinceCode` = VALUES(`StateProvinceCode`),
  `TerritoryID` = VALUES(`TerritoryID`);
