SET @json_data = '{{Production_UnitMeasure.tabledata}}';

INSERT INTO `adventureworks`.`Production_UnitMeasure` (`ModifiedDate`, `Name`, `UnitMeasureCode`)
SELECT `ModifiedDate`, `Name`, `UnitMeasureCode`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name',
    `UnitMeasureCode` CHAR(3) PATH '$.UnitMeasureCode'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
