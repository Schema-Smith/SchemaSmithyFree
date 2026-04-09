SET @json_data = '{{Shippers.tabledata}}';

INSERT INTO `northwind`.`Shippers` (`CompanyName`, `Phone`, `ShipperID`)
SELECT `CompanyName`, `Phone`, `ShipperID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CompanyName` VARCHAR(40) PATH '$.CompanyName',
    `Phone` VARCHAR(24) PATH '$.Phone',
    `ShipperID` INT PATH '$.ShipperID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CompanyName` = VALUES(`CompanyName`),
  `Phone` = VALUES(`Phone`);
