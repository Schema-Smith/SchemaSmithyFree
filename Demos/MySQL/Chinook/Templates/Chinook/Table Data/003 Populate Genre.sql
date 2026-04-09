SET @json_data = '{{Genre.tabledata}}';

INSERT INTO `Chinook`.`Genre` (`GenreId`, `Name`)
SELECT `GenreId`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `GenreId` INT PATH '$.GenreId',
    `Name` VARCHAR(120) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Name` = VALUES(`Name`);
