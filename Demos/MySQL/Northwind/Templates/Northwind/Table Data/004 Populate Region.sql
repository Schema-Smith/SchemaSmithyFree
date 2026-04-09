SET @json_data = '{{Region.tabledata}}';

INSERT INTO `northwind`.`Region` (`RegionDescription`, `RegionID`)
SELECT `RegionDescription`, `RegionID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `RegionDescription` CHAR(50) PATH '$.RegionDescription',
    `RegionID` INT PATH '$.RegionID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `RegionDescription` = VALUES(`RegionDescription`);
