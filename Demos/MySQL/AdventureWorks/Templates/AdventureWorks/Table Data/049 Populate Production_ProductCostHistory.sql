SET @json_data = '{{Production_ProductCostHistory.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductCostHistory` (`EndDate`, `ModifiedDate`, `ProductID`, `StandardCost`, `StartDate`)
SELECT `EndDate`, `ModifiedDate`, `ProductID`, `StandardCost`, `StartDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `EndDate` DATETIME PATH '$.EndDate',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID',
    `StandardCost` DECIMAL(19,4) PATH '$.StandardCost',
    `StartDate` DATETIME PATH '$.StartDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `EndDate` = VALUES(`EndDate`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `StandardCost` = VALUES(`StandardCost`);
