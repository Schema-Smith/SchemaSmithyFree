SET @json_data = '{{Sales_SalesOrderDetail.tabledata}}';

INSERT INTO `adventureworks`.`Sales_SalesOrderDetail` (`CarrierTrackingNumber`, `ModifiedDate`, `OrderQty`, `ProductID`, `rowguid`, `SalesOrderDetailID`, `SalesOrderID`, `SpecialOfferID`, `UnitPrice`, `UnitPriceDiscount`)
SELECT `CarrierTrackingNumber`, `ModifiedDate`, `OrderQty`, `ProductID`, `rowguid`, `SalesOrderDetailID`, `SalesOrderID`, `SpecialOfferID`, `UnitPrice`, `UnitPriceDiscount`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `CarrierTrackingNumber` VARCHAR(25) PATH '$.CarrierTrackingNumber',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `OrderQty` SMALLINT PATH '$.OrderQty',
    `ProductID` INT PATH '$.ProductID',
    `rowguid` CHAR(36) PATH '$.rowguid',
    `SalesOrderDetailID` INT PATH '$.SalesOrderDetailID',
    `SalesOrderID` INT PATH '$.SalesOrderID',
    `SpecialOfferID` INT PATH '$.SpecialOfferID',
    `UnitPrice` DECIMAL(19,4) PATH '$.UnitPrice',
    `UnitPriceDiscount` DECIMAL(19,4) PATH '$.UnitPriceDiscount'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `CarrierTrackingNumber` = VALUES(`CarrierTrackingNumber`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `OrderQty` = VALUES(`OrderQty`),
  `ProductID` = VALUES(`ProductID`),
  `rowguid` = VALUES(`rowguid`),
  `SpecialOfferID` = VALUES(`SpecialOfferID`),
  `UnitPrice` = VALUES(`UnitPrice`),
  `UnitPriceDiscount` = VALUES(`UnitPriceDiscount`);
