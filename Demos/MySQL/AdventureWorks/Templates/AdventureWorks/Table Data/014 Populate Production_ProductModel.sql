SET @json_data = '{{Production_ProductModel.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductModel` (`CatalogDescription`, `Instructions`, `ModifiedDate`, `Name`, `ProductModelID`, `rowguid`)
SELECT `CatalogDescription`, `Instructions`, `ModifiedDate`, `Name`, `ProductModelID`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CatalogDescription` TEXT PATH '$.CatalogDescription',
    `Instructions` TEXT PATH '$.Instructions',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `ProductModelID` INT PATH '$.ProductModelID',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CatalogDescription` = VALUES(`CatalogDescription`),
  `Instructions` = VALUES(`Instructions`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`);
