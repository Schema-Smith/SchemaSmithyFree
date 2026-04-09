SET @json_data = '{{PlaylistTrack.tabledata}}';

INSERT INTO `Chinook`.`PlaylistTrack` (`PlaylistId`, `TrackId`)
SELECT `PlaylistId`, `TrackId`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `PlaylistId` INT PATH '$.PlaylistId',
    `TrackId` INT PATH '$.TrackId'
  )
) AS jt
ON DUPLICATE KEY UPDATE `PlaylistId` = VALUES(`PlaylistId`);
