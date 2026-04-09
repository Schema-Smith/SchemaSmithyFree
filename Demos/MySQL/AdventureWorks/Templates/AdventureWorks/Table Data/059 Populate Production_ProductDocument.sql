SET @json_data = '{{Production_ProductDocument.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductDocument` (`DocumentNode`, `ModifiedDate`, `ProductID`)
SELECT `DocumentNode`, `ModifiedDate`, `ProductID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `DocumentNode` VARCHAR(255) PATH '$.DocumentNode',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`);
