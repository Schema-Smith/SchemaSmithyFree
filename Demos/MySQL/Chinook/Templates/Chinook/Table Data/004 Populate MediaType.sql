SET @json_data = '{{MediaType.tabledata}}';

INSERT INTO `Chinook`.`MediaType` (`MediaTypeId`, `Name`)
SELECT `MediaTypeId`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `MediaTypeId` INT PATH '$.MediaTypeId',
    `Name` VARCHAR(120) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Name` = VALUES(`Name`);
