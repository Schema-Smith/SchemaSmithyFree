SET @json_data = '{{Production_ProductProductPhoto.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductProductPhoto` (`ModifiedDate`, `Primary`, `ProductID`, `ProductPhotoID`)
SELECT `ModifiedDate`, `Primary`, `ProductID`, `ProductPhotoID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Primary` TINYINT PATH '$.Primary',
    `ProductID` INT PATH '$.ProductID',
    `ProductPhotoID` INT PATH '$.ProductPhotoID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Primary` = VALUES(`Primary`);
