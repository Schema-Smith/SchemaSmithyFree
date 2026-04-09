SET @json_data = '{{Production_Location.tabledata}}';

INSERT INTO `adventureworks`.`Production_Location` (`Availability`, `CostRate`, `LocationID`, `ModifiedDate`, `Name`)
SELECT `Availability`, `CostRate`, `LocationID`, `ModifiedDate`, `Name`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Availability` DECIMAL(8,2) PATH '$.Availability',
    `CostRate` DECIMAL(10,4) PATH '$.CostRate',
    `LocationID` SMALLINT PATH '$.LocationID',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `Name` VARCHAR(50) PATH '$.Name'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Availability` = VALUES(`Availability`),
  `CostRate` = VALUES(`CostRate`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `Name` = VALUES(`Name`);
