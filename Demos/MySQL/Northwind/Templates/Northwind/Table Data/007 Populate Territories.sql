SET @json_data = '{{Territories.tabledata}}';

INSERT INTO `northwind`.`Territories` (`RegionID`, `TerritoryDescription`, `TerritoryID`)
SELECT `RegionID`, `TerritoryDescription`, `TerritoryID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `RegionID` INT PATH '$.RegionID',
    `TerritoryDescription` CHAR(50) PATH '$.TerritoryDescription',
    `TerritoryID` VARCHAR(20) PATH '$.TerritoryID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `RegionID` = VALUES(`RegionID`),
  `TerritoryDescription` = VALUES(`TerritoryDescription`);
