SET @json_data = '{{Production_Culture.tabledata}}';

INSERT INTO `adventureworks`.`Production_Culture` (`CultureID`, `ModifiedDate`, `Name`)
SELECT `CultureID`, `ModifiedDate`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CultureID` CHAR(6) PATH '$.CultureID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
