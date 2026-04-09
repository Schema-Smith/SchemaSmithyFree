SET @json_data = '{{Production_ProductCategory.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductCategory` (`ModifiedDate`, `Name`, `ProductCategoryID`, `rowguid`)
SELECT `ModifiedDate`, `Name`, `ProductCategoryID`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `ProductCategoryID` INT PATH '$.ProductCategoryID',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `rowguid` = VALUES(`rowguid`);
