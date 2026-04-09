SET @json_data = '{{Artist.tabledata}}';

INSERT INTO `Chinook`.`Artist` (`ArtistId`, `Name`)
SELECT `ArtistId`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ArtistId` INT PATH '$.ArtistId',
    `Name` VARCHAR(120) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Name` = VALUES(`Name`);
