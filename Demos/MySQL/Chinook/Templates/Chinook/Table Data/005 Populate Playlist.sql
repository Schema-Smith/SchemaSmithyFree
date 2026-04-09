SET @json_data = '{{Playlist.tabledata}}';

INSERT INTO `Chinook`.`Playlist` (`PlaylistId`, `Name`)
SELECT `PlaylistId`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `PlaylistId` INT PATH '$.PlaylistId',
    `Name` VARCHAR(120) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Name` = VALUES(`Name`);
