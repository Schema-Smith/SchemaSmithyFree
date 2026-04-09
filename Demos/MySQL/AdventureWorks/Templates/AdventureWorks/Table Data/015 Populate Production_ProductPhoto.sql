SET @json_data = '{{Production_ProductPhoto.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductPhoto` (`LargePhoto`, `LargePhotoFileName`, `ModifiedDate`, `ProductPhotoID`, `ThumbNailPhoto`, `ThumbnailPhotoFileName`)
SELECT FROM_BASE64(`LargePhoto`), `LargePhotoFileName`, `ModifiedDate`, `ProductPhotoID`, FROM_BASE64(`ThumbNailPhoto`), `ThumbnailPhotoFileName`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `LargePhoto` TEXT PATH '$.LargePhoto',
    `LargePhotoFileName` VARCHAR(50) PATH '$.LargePhotoFileName',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductPhotoID` INT PATH '$.ProductPhotoID',
    `ThumbNailPhoto` TEXT PATH '$.ThumbNailPhoto',
    `ThumbnailPhotoFileName` VARCHAR(50) PATH '$.ThumbnailPhotoFileName'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `LargePhoto` = VALUES(`LargePhoto`),
  `LargePhotoFileName` = VALUES(`LargePhotoFileName`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `ThumbNailPhoto` = VALUES(`ThumbNailPhoto`),
  `ThumbnailPhotoFileName` = VALUES(`ThumbnailPhotoFileName`);
