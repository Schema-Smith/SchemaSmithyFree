SET @json_data = '{{Production_Illustration.tabledata}}';

INSERT INTO `adventureworks`.`Production_Illustration` (`Diagram`, `IllustrationID`, `ModifiedDate`)
SELECT `Diagram`, `IllustrationID`, `ModifiedDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Diagram` TEXT PATH '$.Diagram',
    `IllustrationID` INT PATH '$.IllustrationID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Diagram` = VALUES(`Diagram`),
  `ModifiedDate` = VALUES(`ModifiedDate`);
