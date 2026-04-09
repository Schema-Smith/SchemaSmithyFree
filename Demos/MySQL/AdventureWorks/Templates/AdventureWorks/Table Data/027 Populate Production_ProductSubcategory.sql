SET @json_data = '{{Production_ProductSubcategory.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductSubcategory` (`ModifiedDate`, `Name`, `ProductCategoryID`, `ProductSubcategoryID`, `rowguid`)
SELECT `ModifiedDate`, `Name`, `ProductCategoryID`, `ProductSubcategoryID`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `ProductCategoryID` INT PATH '$.ProductCategoryID',
    `ProductSubcategoryID` INT PATH '$.ProductSubcategoryID',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`),
  `ProductCategoryID` = VALUES(`ProductCategoryID`),
  `rowguid` = VALUES(`rowguid`);
