SET @json_data = '{{Production_ProductModelProductDescriptionCulture.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductModelProductDescriptionCulture` (`CultureID`, `ModifiedDate`, `ProductDescriptionID`, `ProductModelID`)
SELECT `CultureID`, `ModifiedDate`, `ProductDescriptionID`, `ProductModelID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CultureID` CHAR(6) PATH '$.CultureID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductDescriptionID` INT PATH '$.ProductDescriptionID',
    `ProductModelID` INT PATH '$.ProductModelID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`);
