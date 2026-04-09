SET @json_data = '{{Album.tabledata}}';

INSERT INTO `Chinook`.`Album` (`AlbumId`, `Title`, `ArtistId`)
SELECT `AlbumId`, `Title`, `ArtistId`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `AlbumId` INT PATH '$.AlbumId',
    `Title` VARCHAR(160) PATH '$.Title',
    `ArtistId` INT PATH '$.ArtistId'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Title` = VALUES(`Title`),
  `ArtistId` = VALUES(`ArtistId`);
