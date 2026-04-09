SET @json_data = '{{Production_ProductModelIllustration.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductModelIllustration` (`IllustrationID`, `ModifiedDate`, `ProductModelID`)
SELECT `IllustrationID`, `ModifiedDate`, `ProductModelID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `IllustrationID` INT PATH '$.IllustrationID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductModelID` INT PATH '$.ProductModelID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`);
