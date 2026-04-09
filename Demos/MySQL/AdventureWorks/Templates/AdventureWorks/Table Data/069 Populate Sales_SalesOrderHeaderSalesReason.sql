SET @json_data = '{{Sales_SalesOrderHeaderSalesReason.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesOrderHeaderSalesReason` (`ModifiedDate`, `SalesOrderID`, `SalesReasonID`)
SELECT `ModifiedDate`, `SalesOrderID`, `SalesReasonID`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `SalesOrderID` INT PATH '$.SalesOrderID',
    `SalesReasonID` INT PATH '$.SalesReasonID'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ModifiedDate` = VALUES(`ModifiedDate`);
