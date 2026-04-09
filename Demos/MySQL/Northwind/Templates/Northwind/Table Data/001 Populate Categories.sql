SET @json_data = '{{Categories.tabledata}}';

INSERT INTO `northwind`.`Categories` (`CategoryID`, `CategoryName`, `Description`, `Picture`)
SELECT `CategoryID`, `CategoryName`, `Description`, FROM_BASE64(`Picture`)
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CategoryID` INT PATH '$.CategoryID',
    `CategoryName` VARCHAR(15) PATH '$.CategoryName',
    `Description` TEXT PATH '$.Description',
    `Picture` TEXT PATH '$.Picture'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CategoryName` = VALUES(`CategoryName`),
  `Description` = VALUES(`Description`),
  `Picture` = VALUES(`Picture`);
