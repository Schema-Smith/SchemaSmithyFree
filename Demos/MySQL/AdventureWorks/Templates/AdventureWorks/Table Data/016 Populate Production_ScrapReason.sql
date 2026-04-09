SET @json_data = '{{Production_ScrapReason.tabledata}}';

INSERT INTO `adventureworks`.`Production_ScrapReason` (`ModifiedDate`, `Name`, `ScrapReasonID`)
SELECT `ModifiedDate`, `Name`, `ScrapReasonID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `ScrapReasonID` SMALLINT PATH '$.ScrapReasonID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
