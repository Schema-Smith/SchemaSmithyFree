SET @json_data = '{{Track.tabledata}}';

INSERT INTO `Chinook`.`Track` (`TrackId`, `Name`, `AlbumId`, `MediaTypeId`, `GenreId`, `Composer`, `Milliseconds`, `Bytes`, `UnitPrice`)
SELECT `TrackId`, `Name`, `AlbumId`, `MediaTypeId`, `GenreId`, `Composer`, `Milliseconds`, `Bytes`, `UnitPrice`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `TrackId` INT PATH '$.TrackId',
    `Name` VARCHAR(200) PATH '$.Name',
    `AlbumId` INT PATH '$.AlbumId',
    `MediaTypeId` INT PATH '$.MediaTypeId',
    `GenreId` INT PATH '$.GenreId',
    `Composer` VARCHAR(220) PATH '$.Composer',
    `Milliseconds` INT PATH '$.Milliseconds',
    `Bytes` INT PATH '$.Bytes',
    `UnitPrice` DECIMAL(10,2) PATH '$.UnitPrice'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Name` = VALUES(`Name`),
  `AlbumId` = VALUES(`AlbumId`),
  `MediaTypeId` = VALUES(`MediaTypeId`),
  `GenreId` = VALUES(`GenreId`),
  `Composer` = VALUES(`Composer`),
  `Milliseconds` = VALUES(`Milliseconds`),
  `Bytes` = VALUES(`Bytes`),
  `UnitPrice` = VALUES(`UnitPrice`);
