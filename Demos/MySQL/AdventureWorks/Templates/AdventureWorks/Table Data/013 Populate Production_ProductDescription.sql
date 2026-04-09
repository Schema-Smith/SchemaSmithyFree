SET @json_data = '{{Production_ProductDescription.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductDescription` (`Description`, `ModifiedDate`, `ProductDescriptionID`, `rowguid`)
SELECT `Description`, `ModifiedDate`, `ProductDescriptionID`, `rowguid`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Description` VARCHAR(400) PATH '$.Description',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductDescriptionID` INT PATH '$.ProductDescriptionID',
    `rowguid` CHAR(36) PATH '$.rowguid'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Description` = VALUES(`Description`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `rowguid` = VALUES(`rowguid`);
