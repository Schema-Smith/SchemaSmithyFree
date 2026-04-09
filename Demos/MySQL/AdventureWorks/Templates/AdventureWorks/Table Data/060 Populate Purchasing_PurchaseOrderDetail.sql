SET @json_data = '{{Purchasing_PurchaseOrderDetail.tabledata}}';

INSERT INTO `adventureworks`.`Purchasing_PurchaseOrderDetail` (`DueDate`, `ModifiedDate`, `OrderQty`, `ProductID`, `PurchaseOrderDetailID`, `PurchaseOrderID`, `ReceivedQty`, `RejectedQty`, `UnitPrice`)
SELECT `DueDate`, `ModifiedDate`, `OrderQty`, `ProductID`, `PurchaseOrderDetailID`, `PurchaseOrderID`, `ReceivedQty`, `RejectedQty`, `UnitPrice`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `DueDate` DATETIME PATH '$.DueDate',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `OrderQty` SMALLINT PATH '$.OrderQty',
    `ProductID` INT PATH '$.ProductID',
    `PurchaseOrderDetailID` INT PATH '$.PurchaseOrderDetailID',
    `PurchaseOrderID` INT PATH '$.PurchaseOrderID',
    `ReceivedQty` DECIMAL(8,2) PATH '$.ReceivedQty',
    `RejectedQty` DECIMAL(8,2) PATH '$.RejectedQty',
    `UnitPrice` DECIMAL(19,4) PATH '$.UnitPrice'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `DueDate` = VALUES(`DueDate`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `OrderQty` = VALUES(`OrderQty`),
  `ProductID` = VALUES(`ProductID`),
  `ReceivedQty` = VALUES(`ReceivedQty`),
  `RejectedQty` = VALUES(`RejectedQty`),
  `UnitPrice` = VALUES(`UnitPrice`);
