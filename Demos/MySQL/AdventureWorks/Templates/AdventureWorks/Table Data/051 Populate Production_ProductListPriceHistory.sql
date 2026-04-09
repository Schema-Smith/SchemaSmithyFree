SET @json_data = '{{Production_ProductListPriceHistory.tabledata}}';

INSERT INTO `adventureworks`.`Production_ProductListPriceHistory` (`EndDate`, `ListPrice`, `ModifiedDate`, `ProductID`, `StartDate`)
SELECT `EndDate`, `ListPrice`, `ModifiedDate`, `ProductID`, `StartDate`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `EndDate` DATETIME PATH '$.EndDate',
    `ListPrice` DECIMAL(19,4) PATH '$.ListPrice',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID',
    `StartDate` DATETIME PATH '$.StartDate'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `EndDate` = VALUES(`EndDate`),
  `ListPrice` = VALUES(`ListPrice`),
  `ModifiedDate` = VALUES(`ModifiedDate`);
