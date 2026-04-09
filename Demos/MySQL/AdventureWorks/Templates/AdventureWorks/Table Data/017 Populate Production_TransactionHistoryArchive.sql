SET @json_data = '{{Production_TransactionHistoryArchive.tabledata}}';

INSERT INTO `adventureworks`.`Production_TransactionHistoryArchive` (`ActualCost`, `ModifiedDate`, `ProductID`, `Quantity`, `ReferenceOrderID`, `ReferenceOrderLineID`, `TransactionDate`, `TransactionID`, `TransactionType`)
SELECT `ActualCost`, `ModifiedDate`, `ProductID`, `Quantity`, `ReferenceOrderID`, `ReferenceOrderLineID`, `TransactionDate`, `TransactionID`, `TransactionType`
FROM JSON_TABLE(
  @json_data,
  '$[*]' COLUMNS (
    `ActualCost` DECIMAL(19,4) PATH '$.ActualCost',
    `ModifiedDate` DATETIME PATH '$.ModifiedDate',
    `ProductID` INT PATH '$.ProductID',
    `Quantity` INT PATH '$.Quantity',
    `ReferenceOrderID` INT PATH '$.ReferenceOrderID',
    `ReferenceOrderLineID` INT PATH '$.ReferenceOrderLineID',
    `TransactionDate` DATETIME PATH '$.TransactionDate',
    `TransactionID` INT PATH '$.TransactionID',
    `TransactionType` CHAR(1) PATH '$.TransactionType'
  )
) AS jt
ON DUPLICATE KEY UPDATE
  `ActualCost` = VALUES(`ActualCost`),
  `ModifiedDate` = VALUES(`ModifiedDate`),
  `ProductID` = VALUES(`ProductID`),
  `Quantity` = VALUES(`Quantity`),
  `ReferenceOrderID` = VALUES(`ReferenceOrderID`),
  `ReferenceOrderLineID` = VALUES(`ReferenceOrderLineID`),
  `TransactionDate` = VALUES(`TransactionDate`),
  `TransactionType` = VALUES(`TransactionType`);
