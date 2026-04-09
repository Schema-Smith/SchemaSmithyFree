SET @json_data = '{{Order Details.tabledata}}';

INSERT INTO `northwind`.`Order Details` (`Discount`, `OrderID`, `ProductID`, `Quantity`, `UnitPrice`)
SELECT `Discount`, `OrderID`, `ProductID`, `Quantity`, `UnitPrice`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `Discount` FLOAT PATH '$.Discount',
    `OrderID` INT PATH '$.OrderID',
    `ProductID` INT PATH '$.ProductID',
    `Quantity` SMALLINT PATH '$.Quantity',
    `UnitPrice` DECIMAL(19,4) PATH '$.UnitPrice'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `Discount` = VALUES(`Discount`),
  `Quantity` = VALUES(`Quantity`),
  `UnitPrice` = VALUES(`UnitPrice`);
